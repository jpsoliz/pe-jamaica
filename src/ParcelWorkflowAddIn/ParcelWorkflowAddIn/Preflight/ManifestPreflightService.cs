using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class ManifestPreflightService
{
    private static readonly JsonSerializerOptions StableHashJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".dwg",
        ".txt",
        ".csv",
        ".tif",
        ".tiff",
        ".png",
        ".jpg",
        ".jpeg"
    };

    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly Func<string> createRunId;
    private readonly IProcessingEnvironmentPreflightService environmentPreflightService;

    public ManifestPreflightService()
        : this(() => DateTimeOffset.UtcNow, () => $"preflight-{Guid.NewGuid():N}", new NoOpProcessingEnvironmentPreflightService())
    {
    }

    public ManifestPreflightService(Func<DateTimeOffset> getUtcNow, Func<string> createRunId)
        : this(getUtcNow, createRunId, new NoOpProcessingEnvironmentPreflightService())
    {
    }

    public ManifestPreflightService(
        Func<DateTimeOffset> getUtcNow,
        Func<string> createRunId,
        IProcessingEnvironmentPreflightService environmentPreflightService)
    {
        this.getUtcNow = getUtcNow;
        this.createRunId = createRunId;
        this.environmentPreflightService = environmentPreflightService;
    }

    public static ManifestPreflightService CreateDefault()
    {
        return new ManifestPreflightService(
            () => DateTimeOffset.UtcNow,
            () => $"preflight-{Guid.NewGuid():N}",
            new ProcessingEnvironmentPreflightService());
    }

    public PreflightSummaryDocument Run(CaseFolderLayout layout, string? createdBy)
    {
        return RunAsync(layout, createdBy).GetAwaiter().GetResult();
    }

    public async Task<PreflightSummaryDocument> RunAsync(CaseFolderLayout layout, string? createdBy, CancellationToken cancellationToken = default)
    {
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        var manifestHash = ComputeSourceManifestHash(manifest);
        var blockers = new List<PreflightCheck>();
        var warnings = new List<PreflightCheck>();
        var passed = new List<PreflightCheck>();

        if (manifest.Payload.DetectedProfile is null)
        {
            blockers.Add(PreflightCheck.Blocker(
                "detected_profile_present",
                "Detected profile is not refreshed.",
                layout.ManifestPath,
                null,
                "Refresh intake before running preflight."));
        }
        else
        {
            EvaluateProfile(manifest, layout, blockers, passed);
        }

        var environmentResult = await environmentPreflightService.RunAsync(layout, cancellationToken).ConfigureAwait(false);
        blockers.AddRange(environmentResult.Blockers);
        warnings.AddRange(environmentResult.Warnings);
        passed.AddRange(environmentResult.PassedChecks);

        var status = blockers.Count > 0 ? "blocked" : "passed";
        var summary = new PreflightSummaryDocument(
            "1.0.0",
            manifest.TransactionId,
            createRunId(),
            getUtcNow().UtcDateTime.ToString("O"),
            createdBy,
            manifestHash,
            new PreflightSummaryPayload(status, blockers, warnings, passed),
            warnings.Select(warning => warning.Message).ToArray(),
            blockers.Select(blocker => blocker.Message).ToArray());

        PreflightSummarySerializer.Write(layout.PreflightSummaryPath, summary);
        return summary;
    }

    private static void EvaluateProfile(
        ManifestDocument manifest,
        CaseFolderLayout layout,
        List<PreflightCheck> blockers,
        List<PreflightCheck> passed)
    {
        var profile = manifest.Payload.DetectedProfile!;
        if (profile.ProfileCode == SourceInputProfile.UnsupportedIntake)
        {
            blockers.Add(PreflightCheck.Blocker(
                "detected_profile_supported",
                "Unsupported intake cannot pass preflight.",
                layout.ManifestPath,
                null,
                "Add supported source files and refresh intake."));
            return;
        }

        if (profile.ProfileCode == SourceInputProfile.IncompleteIntake)
        {
            blockers.Add(PreflightCheck.Blocker(
                "detected_profile_complete",
                "Incomplete intake cannot pass preflight.",
                layout.ManifestPath,
                null,
                "Resolve missing intake roles and refresh intake."));
        }

        foreach (var role in RequiredRoles(profile.ProfileCode))
        {
            EvaluateRequiredRole(manifest.Payload.SourceFiles, layout, role, blockers, passed);
        }
    }

    private static void EvaluateRequiredRole(
        IReadOnlyList<ManifestSourceFile> sources,
        CaseFolderLayout layout,
        string role,
        List<PreflightCheck> blockers,
        List<PreflightCheck> passed)
    {
        var source = sources.FirstOrDefault(item => string.Equals(item.SourceRole, role, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            blockers.Add(PreflightCheck.Blocker(
                $"required_role_{role}",
                MissingRoleMessage(role),
                null,
                role,
                $"Add a copied {RoleDisplayName(role)} source file and refresh intake."));
            return;
        }

        passed.Add(PreflightCheck.Passed(
            $"required_role_{role}",
            $"Passed: required {RoleDisplayName(role)} source role is present.",
            source.CopiedPath,
            role));

        ValidateCopiedSource(layout, source, role, blockers, passed);
    }

    private static void ValidateCopiedSource(
        CaseFolderLayout layout,
        ManifestSourceFile source,
        string role,
        List<PreflightCheck> blockers,
        List<PreflightCheck> passed)
    {
        if (string.IsNullOrWhiteSpace(source.CopiedPath))
        {
            blockers.Add(PreflightCheck.Blocker(
                $"source_file_copied_path_{role}",
                $"Missing copied path for {RoleDisplayName(role)}.",
                null,
                role,
                "Re-add the source file to the Case Folder."));
            return;
        }

        string copiedPath;
        try
        {
            copiedPath = Path.GetFullPath(source.CopiedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            blockers.Add(PreflightCheck.Blocker(
                $"source_file_path_readable_{role}",
                $"Copied source path could not be read: {exception.Message}",
                source.CopiedPath,
                role,
                "Re-add the source file to the Case Folder."));
            return;
        }

        if (!IsPathInside(layout.SourceDirectory, copiedPath))
        {
            blockers.Add(PreflightCheck.Blocker(
                $"source_file_contained_{role}",
                "Copied source path is outside the Case Folder source area.",
                copiedPath,
                role,
                "Re-add the source file from the dock pane."));
            return;
        }

        passed.Add(PreflightCheck.Passed(
            $"source_file_contained_{role}",
            $"Passed: {RoleDisplayName(role)} copied path is inside the Case Folder source area.",
            copiedPath,
            role));

        if (!File.Exists(copiedPath))
        {
            blockers.Add(PreflightCheck.Blocker(
                $"source_file_exists_{role}",
                "Copied source file is missing from the Case Folder.",
                copiedPath,
                role,
                "Re-add the missing source file."));
            return;
        }

        passed.Add(PreflightCheck.Passed(
            $"source_file_exists_{role}",
            $"Passed: {RoleDisplayName(role)} copied file exists.",
            copiedPath,
            role));

        var extension = Path.GetExtension(copiedPath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension) || !string.Equals(extension, source.FileType, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(PreflightCheck.Blocker(
                $"source_file_extension_{role}",
                $"Unsupported source file type: {source.FileType}.",
                copiedPath,
                role,
                "Replace the source with a supported PDF, DWG, TXT, CSV, TIF, PNG, JPG, JPEG, or TIFF file."));
            return;
        }

        passed.Add(PreflightCheck.Passed(
            $"source_file_extension_{role}",
            $"Passed: {RoleDisplayName(role)} source file type is supported.",
            copiedPath,
            role));

        try
        {
            using var stream = File.Open(copiedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            blockers.Add(PreflightCheck.Blocker(
                $"source_file_readable_{role}",
                $"Copied source file could not be read: {exception.Message}",
                copiedPath,
                role,
                "Close locking applications or re-add a readable source file."));
            return;
        }

        passed.Add(PreflightCheck.Passed(
            $"source_file_readable_{role}",
            $"Passed: {RoleDisplayName(role)} copied file is readable.",
            copiedPath,
            role));
    }

    private static IReadOnlyList<string> RequiredRoles(string profileCode)
    {
        return profileCode switch
        {
            SourceInputProfile.ScenarioA => new[] { "computation_source", "plan_map_reference" },
            SourceInputProfile.ScenarioB => new[] { "points_computation", "dwg_reference", "plan_map_reference" },
            _ => Array.Empty<string>()
        };
    }

    private static string MissingRoleMessage(string role)
    {
        return role switch
        {
            "plan_map_reference" => "Missing plan/map reference.",
            "computation_source" => "Missing computation source.",
            "points_computation" => "Missing points/computation source.",
            "dwg_reference" => "Missing DWG reference.",
            _ => $"Missing required source role: {role}."
        };
    }

    private static string RoleDisplayName(string role)
    {
        return role switch
        {
            "plan_map_reference" => "plan/map reference",
            "computation_source" => "computation source",
            "points_computation" => "points/computation source",
            "dwg_reference" => "DWG reference",
            _ => role
        };
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSourceManifestHash(ManifestDocument manifest)
    {
        var stableManifest = manifest with
        {
            Payload = manifest.Payload with { WorkflowState = string.Empty }
        };
        var serialized = JsonSerializer.Serialize(stableManifest, StableHashJsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
