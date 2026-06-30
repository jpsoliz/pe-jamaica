using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.WorkflowRules;

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
    private readonly IDwgReferenceReadinessInspector dwgReadinessInspector;
    private readonly PreflightRuleCatalog ruleCatalog;
    private const int MinimumDwgFileSizeBytes = 1;
    private const double JamaicaMinimumEasting = 550000d;
    private const double JamaicaMaximumEasting = 900000d;
    private const double JamaicaMinimumNorthing = 550000d;
    private const double JamaicaMaximumNorthing = 800000d;

    public ManifestPreflightService()
        : this(() => DateTimeOffset.UtcNow, () => $"preflight-{Guid.NewGuid():N}", new NoOpProcessingEnvironmentPreflightService(), new NoOpDwgReferenceReadinessInspector(), new PreflightRuleCatalogLoader().Load())
    {
    }

    public ManifestPreflightService(Func<DateTimeOffset> getUtcNow, Func<string> createRunId)
        : this(getUtcNow, createRunId, new NoOpProcessingEnvironmentPreflightService(), new NoOpDwgReferenceReadinessInspector(), new PreflightRuleCatalogLoader().Load())
    {
    }

    public ManifestPreflightService(
        Func<DateTimeOffset> getUtcNow,
        Func<string> createRunId,
        IProcessingEnvironmentPreflightService environmentPreflightService)
        : this(getUtcNow, createRunId, environmentPreflightService, new NoOpDwgReferenceReadinessInspector(), new PreflightRuleCatalogLoader().Load())
    {
    }

    public ManifestPreflightService(
        Func<DateTimeOffset> getUtcNow,
        Func<string> createRunId,
        IProcessingEnvironmentPreflightService environmentPreflightService,
        IDwgReferenceReadinessInspector dwgReadinessInspector,
        PreflightRuleCatalog? ruleCatalog = null)
    {
        this.getUtcNow = getUtcNow;
        this.createRunId = createRunId;
        this.environmentPreflightService = environmentPreflightService;
        this.dwgReadinessInspector = dwgReadinessInspector;
        this.ruleCatalog = ruleCatalog ?? new PreflightRuleCatalogLoader().Load();
    }

    public static ManifestPreflightService CreateDefault()
    {
        return new ManifestPreflightService(
            () => DateTimeOffset.UtcNow,
            () => $"preflight-{Guid.NewGuid():N}",
            new ProcessingEnvironmentPreflightService(),
            new ArcPyDwgReferenceReadinessInspector(new ProcessRunner()),
            new PreflightRuleCatalogLoader().Load());
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
                "Detected profile is not refreshed for Supporting Document Check.",
                layout.ManifestPath,
                null,
                "Refresh intake before running preflight."));
        }
        else
        {
            await EvaluateProfile(manifest, layout, blockers, warnings, passed, cancellationToken).ConfigureAwait(false);
        }

        EvaluateGeoreferenceReadiness(manifest, layout, blockers, warnings, passed);

        EvaluateScriptPlan(manifest, layout, blockers, passed);

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

    private async Task EvaluateProfile(
        ManifestDocument manifest,
        CaseFolderLayout layout,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed,
        CancellationToken cancellationToken)
    {
        var profile = manifest.Payload.DetectedProfile!;
        if (profile.ProfileCode == SourceInputProfile.UnsupportedIntake)
        {
            blockers.Add(PreflightCheck.Blocker(
                "detected_profile_supported",
                "Unsupported supporting documents cannot pass Structure Check.",
                layout.ManifestPath,
                null,
                "Add supported source files and refresh Supporting Document Check."));
            return;
        }

        if (profile.ProfileCode == SourceInputProfile.IncompleteIntake)
        {
            blockers.Add(PreflightCheck.Blocker(
                "detected_profile_complete",
                "Supporting Document Check is incomplete.",
                layout.ManifestPath,
                null,
                "Resolve missing supporting document roles and refresh intake."));
        }

        EvaluateSupportingDocumentInventory(manifest, blockers, warnings, passed);

        var requiredRoles = GetRequiredRoles(manifest, profile.ProfileCode);
        foreach (var role in requiredRoles)
        {
            await EvaluateRequiredRole(manifest.Payload.SourceFiles, layout, role, blockers, warnings, passed, cancellationToken).ConfigureAwait(false);
        }

        await EvaluateOptionalDwgSources(manifest.Payload.SourceFiles, layout, requiredRoles, blockers, warnings, passed, cancellationToken).ConfigureAwait(false);
    }

    private static void EvaluateScriptPlan(
        ManifestDocument manifest,
        CaseFolderLayout layout,
        List<PreflightCheck> blockers,
        List<PreflightCheck> passed)
    {
        if (manifest.Payload.InnolaTransaction is null)
        {
            return;
        }

        if (manifest.Payload.ScriptPlan is null
            || string.IsNullOrWhiteSpace(manifest.Payload.WorkflowRuleId)
            || string.IsNullOrWhiteSpace(manifest.Payload.WorkflowProfile))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "workflow_rule",
                "workflow_rule_resolved",
                "No workflow rule matches the transaction type and copied supporting documents.",
                layout.ManifestPath,
                null,
                "Review transaction type and attached source file roles, then reload the transaction."));
            return;
        }

        var effectiveSourceFiles = SupportingDocumentSourceFilter.Apply(
            manifest.Payload.SourceFiles,
            manifest.Payload.SupportingDocumentOptions);
        var currentHash = WorkflowRuleResolver.ComputeSourceManifestHash(effectiveSourceFiles);
        if (!string.Equals(currentHash, manifest.Payload.ScriptPlan.SourceManifestHash, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "workflow_rule",
                "script_plan_current",
                "Script plan is stale for the current source files.",
                layout.ManifestPath,
                null,
                "Refresh intake or reload the transaction to resolve a new script plan."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "workflow_rule",
            "workflow_rule_resolved",
            $"Passed: workflow rule {manifest.Payload.WorkflowRuleId} resolved.",
            layout.ManifestPath,
            null));
    }

    private void EvaluateGeoreferenceReadiness(
        ManifestDocument manifest,
        CaseFolderLayout layout,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed)
    {
        var sources = manifest.Payload.SourceFiles;
        var georeferenceSourceRule = ruleCatalog.TryGetRule("georeference_source_presence");
        if (georeferenceSourceRule is { Enabled: true })
        {
            var georeferenceSource = sources.FirstOrDefault(source => RuleAppliesToSource(georeferenceSourceRule, source));
            if (georeferenceSource is null)
            {
                AddRuleIssue(
                    georeferenceSourceRule,
                    blockers,
                    warnings,
                    "No source with usable coordinate context is present for Georeference Check.",
                    layout.ManifestPath,
                    null,
                    "Add a computation sheet, tabular coordinate source, or map reference with usable coordinate context.");
            }
            else
            {
                passed.Add(PreflightCheck.PassedForCategory(
                    georeferenceSourceRule.Category,
                    georeferenceSourceRule.RuleId,
                    $"Passed: {RoleDisplayName(georeferenceSource.SourceRole ?? string.Empty)} is available for Georeference Check.",
                    georeferenceSource.CopiedPath,
                    georeferenceSource.SourceRole));
            }
        }

        var tabularRule = ruleCatalog.TryGetRule("tabular_coordinate_columns");
        if (tabularRule is { Enabled: true })
        {
            foreach (var source in sources.Where(source => RuleAppliesToSource(tabularRule, source)))
            {
                if (!TryReadTabularCoordinates(source.CopiedPath, out var result))
                {
                    AddRuleIssue(
                        tabularRule,
                        blockers,
                        warnings,
                        $"Tabular georeference columns could not be verified for {Path.GetFileName(source.CopiedPath)}.",
                        source.CopiedPath,
                        source.SourceRole,
                        "Verify the TXT/CSV source exposes Easting and Northing columns.");
                    continue;
                }

                if (!result.HasCoordinateColumns)
                {
                    AddRuleIssue(
                        tabularRule,
                        blockers,
                        warnings,
                        $"TXT/CSV source {Path.GetFileName(source.CopiedPath)} is missing Easting/Northing-style columns.",
                        source.CopiedPath,
                        source.SourceRole,
                        "Rename or add Easting/Northing columns before rerunning Structure Check.");
                    continue;
                }

                passed.Add(PreflightCheck.PassedForCategory(
                    tabularRule.Category,
                    tabularRule.RuleId,
                    $"Passed: {Path.GetFileName(source.CopiedPath)} exposes tabular coordinate columns for Georeference Check.",
                    source.CopiedPath,
                    source.SourceRole));

                var boundsRule = ruleCatalog.TryGetRule("jamaica_coordinate_bounds");
                if (boundsRule is not { Enabled: true } || !RuleAppliesToSource(boundsRule, source))
                {
                    continue;
                }

                if (result.SampleCoordinate is null)
                {
                    AddRuleIssue(
                        boundsRule,
                        blockers,
                        warnings,
                        $"Jamaica coordinate bounds could not be sampled from {Path.GetFileName(source.CopiedPath)}.",
                        source.CopiedPath,
                        source.SourceRole,
                        "Add at least one numeric Easting/Northing row before rerunning Georeference Check.");
                    continue;
                }

                var sample = result.SampleCoordinate.Value;
                if (sample.Easting < JamaicaMinimumEasting
                    || sample.Easting > JamaicaMaximumEasting
                    || sample.Northing < JamaicaMinimumNorthing
                    || sample.Northing > JamaicaMaximumNorthing)
                {
                    AddRuleIssue(
                        boundsRule,
                        blockers,
                        warnings,
                        $"Sample coordinates from {Path.GetFileName(source.CopiedPath)} fall outside the configured Jamaica working bounds.",
                        source.CopiedPath,
                        source.SourceRole,
                        "Check the coordinate system, units, and source file values before rerunning Georeference Check.");
                    continue;
                }

                passed.Add(PreflightCheck.PassedForCategory(
                    boundsRule.Category,
                    boundsRule.RuleId,
                    $"Passed: sample coordinates from {Path.GetFileName(source.CopiedPath)} fall within Jamaica working bounds.",
                    source.CopiedPath,
                    source.SourceRole));
            }
        }
    }

    private async Task EvaluateRequiredRole(
        IReadOnlyList<ManifestSourceFile> sources,
        CaseFolderLayout layout,
        string role,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed,
        CancellationToken cancellationToken)
    {
        var source = sources.FirstOrDefault(item => SourceRole.Matches(item.SourceRole, role));
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
            $"Passed: {RoleDisplayName(role)} found - {Path.GetFileName(source.CopiedPath)} ({source.FileType}).",
            source.CopiedPath,
            role));

        await ValidateCopiedSource(layout, source, role, blockers, warnings, passed, cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateCopiedSource(
        CaseFolderLayout layout,
        ManifestSourceFile source,
        string role,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed,
        CancellationToken cancellationToken)
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

        if (SourceRole.Matches(role, SourceRole.DwgSource))
        {
            await ValidateDwgReadiness(source, copiedPath, blockers, warnings, passed, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EvaluateOptionalDwgSources(
        IReadOnlyList<ManifestSourceFile> sources,
        CaseFolderLayout layout,
        IReadOnlyList<string> requiredRoles,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed,
        CancellationToken cancellationToken)
    {
        if (SourceRole.MatchesAny(SourceRole.DwgSource, requiredRoles))
        {
            return;
        }

        foreach (var source in sources.Where(source => SourceRole.Matches(source.SourceRole, SourceRole.DwgSource)))
        {
            await ValidateCopiedSource(layout, source, SourceRole.DwgSource, blockers, warnings, passed, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ValidateDwgReadiness(
        ManifestSourceFile source,
        string copiedPath,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed,
        CancellationToken cancellationToken)
    {
        passed.Add(PreflightCheck.PassedForCategory(
            "dwg",
            "dwg_source_present",
            $"Passed: DWG source was detected for role {source.SourceRole}.",
            copiedPath,
            source.SourceRole));

        var fileInfo = new FileInfo(copiedPath);
        if (fileInfo.Length < MinimumDwgFileSizeBytes)
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "dwg",
                "dwg_source_non_empty",
                "DWG source file is empty.",
                copiedPath,
                source.SourceRole,
                "Upload a non-empty DWG source file."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "dwg",
            "dwg_source_readable",
            "Passed: DWG source file is non-empty and readable.",
            copiedPath,
            source.SourceRole));

        if (!IsLikelyDwgFile(copiedPath))
        {
            blockers.Add(PreflightCheck.BlockerForCategory(
                "dwg",
                "dwg_source_signature",
                "DWG source file does not appear to be a valid DWG.",
                copiedPath,
                source.SourceRole,
                "Upload a valid DWG reference file."));
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "dwg",
            "dwg_source_signature",
            "Passed: DWG source signature is present.",
            copiedPath,
            source.SourceRole));

        var probeResult = await dwgReadinessInspector.InspectAsync(copiedPath, cancellationToken).ConfigureAwait(false);
        var rule = ruleCatalog.GetRule("dwg_readiness_probe");
        if (!rule.Enabled)
        {
            warnings.Add(PreflightCheck.DisabledForCategory(
                rule.Category,
                rule.RuleId,
                $"Skipped: {rule.DisplayName} is disabled in PreflightRules.json.",
                ruleCatalog.SourcePath,
                source.SourceRole));
            return;
        }

        if (!probeResult.ProbeExecuted)
        {
            return;
        }

        if (!probeResult.Success)
        {
            var check = PreflightCheck.BlockerForCategory(
                "dwg",
                "dwg_source_sublayers",
                probeResult.Message ?? "DWG source has no readable CAD sub-layers.",
                copiedPath,
                source.SourceRole,
                probeResult.Correction);
            var severity = PreflightRuleDefinition.NormalizeSeverity(rule.Severity, "blocker");
            if (severity == "warning")
            {
                warnings.Add(PreflightCheck.WarningForCategory(
                    "dwg",
                    "dwg_source_sublayers",
                    check.Message,
                    copiedPath,
                    source.SourceRole,
                    probeResult.Correction));
            }
            else
            {
                blockers.Add(check);
            }
            return;
        }

        passed.Add(PreflightCheck.PassedForCategory(
            "dwg",
            "dwg_source_sublayers",
            "Passed: DWG source contains readable CAD sub-layers.",
            copiedPath,
            source.SourceRole));
    }

    private static bool IsLikelyDwgFile(string copiedPath)
    {
        try
        {
            using var stream = File.OpenRead(copiedPath);
            var maxSignatureBytes = (int)Math.Min(stream.Length, 64L);
            var buffer = new byte[maxSignatureBytes];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read < 4)
            {
                return false;
            }

            var signature = Encoding.ASCII.GetString(buffer, 0, read).ToUpperInvariant();
            return signature.Contains("AC1", StringComparison.Ordinal) || signature.Contains("ACAD", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> RequiredRoles(string profileCode)
    {
        return profileCode switch
        {
            SourceInputProfile.ScenarioA => ComputeAttachmentSourceTypeCatalog.RequiredWorkflowRoles,
            SourceInputProfile.ScenarioB => ComputeAttachmentSourceTypeCatalog.RequiredWorkflowRoles,
            _ => Array.Empty<string>()
        };
    }

    private IReadOnlyList<string> GetRequiredRoles(ManifestDocument manifest, string profileCode)
    {
        var rule = ruleCatalog.TryGetRule("required_source_roles");
        var transactionType = manifest.Payload.InnolaTransaction?.CaseType;
        var workflowStage = manifest.Payload.InnolaTransaction?.TaskName;
        if (rule is { Enabled: true }
            && rule.AppliesToTransaction(transactionType, workflowStage)
            && rule.SourceRoles is { Count: > 0 })
        {
            return rule.SourceRoles;
        }

        return RequiredRoles(profileCode);
    }

    private static string MissingRoleMessage(string role)
    {
        return SourceRole.Normalize(role) switch
        {
            SourceRole.PlanMapReference => "Survey plan / map reference: missing.",
            SourceRole.ComputationSheet => "Survey / computation sheet: missing.",
            SourceRole.CoordinateTextSource => "Structured survey points: missing.",
            SourceRole.DwgSource => "AutoCAD survey source: missing.",
            _ => $"Missing required source role: {role}."
        };
    }

    private static string RoleDisplayName(string role)
    {
        return SourceRole.DisplayName(role);
    }

    private static void EvaluateSupportingDocumentInventory(
        ManifestDocument manifest,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        List<PreflightCheck> passed)
    {
        foreach (var definition in ComputeAttachmentSourceTypeCatalog.SafeDefaults.Where(item => !item.InternalOnly && !item.Required))
        {
            var source = ResolveSourceForDefinition(manifest, definition);
            var checkId = $"supporting_document_{definition.SourceType}";
            if (source is not null)
            {
                passed.Add(PreflightCheck.PassedForCategory(
                    "supporting_document",
                    checkId,
                    $"{definition.DisplayName}: found - {Path.GetFileName(source.CopiedPath)} ({source.FileType}).",
                    source.CopiedPath,
                    definition.WorkflowRole));
                continue;
            }

            passed.Add(PreflightCheck.PassedForCategory(
                "supporting_document",
                checkId,
                $"{definition.DisplayName}: not provided (optional).",
                null,
                definition.WorkflowRole));
        }
    }

    private static ManifestSourceFile? ResolveSourceForDefinition(ManifestDocument manifest, ComputeAttachmentSourceTypeDefinition definition)
    {
        var directMatch = manifest.Payload.SourceFiles.FirstOrDefault(source => SourceRole.Matches(source.SourceRole, definition.WorkflowRole)
            || string.Equals(source.SourceType, definition.SourceType, StringComparison.OrdinalIgnoreCase));
        if (directMatch is not null)
        {
            return directMatch;
        }

        if (manifest.Payload.AttachmentProvenance is null)
        {
            return null;
        }

        var matchedProvenance = manifest.Payload.AttachmentProvenance.FirstOrDefault(attachment =>
            SourceRole.Matches(attachment.SourceRole, definition.WorkflowRole)
            || string.Equals(attachment.SourceType, definition.SourceType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(attachment.Category, definition.SourceType, StringComparison.OrdinalIgnoreCase));

        if (matchedProvenance is null)
        {
            return null;
        }

        return manifest.Payload.SourceFiles.FirstOrDefault(source =>
            string.Equals(source.CopiedPath, matchedProvenance.CopiedPath, StringComparison.OrdinalIgnoreCase))
            ?? new ManifestSourceFile(
                matchedProvenance.ServiceReference,
                matchedProvenance.CopiedPath,
                matchedProvenance.Extension,
                matchedProvenance.FileSize ?? 0,
                matchedProvenance.CopiedAt,
                matchedProvenance.SourceRole,
                matchedProvenance.SourceType ?? matchedProvenance.Category);
    }

    private static bool RuleAppliesToSource(PreflightRuleDefinition rule, ManifestSourceFile source)
    {
        return rule.AppliesToSource(source.SourceRole, source.FileType);
    }

    private static void AddRuleIssue(
        PreflightRuleDefinition rule,
        List<PreflightCheck> blockers,
        List<PreflightCheck> warnings,
        string message,
        string? affectedPath,
        string? sourceRole,
        string? correction)
    {
        var severity = PreflightRuleDefinition.NormalizeSeverity(rule.Severity, "warning");
        if (severity == "blocker")
        {
            blockers.Add(PreflightCheck.BlockerForCategory(rule.Category, rule.RuleId, message, affectedPath, sourceRole, correction));
            return;
        }

        warnings.Add(PreflightCheck.WarningForCategory(rule.Category, rule.RuleId, message, affectedPath, sourceRole, correction));
    }

    private static bool TryReadTabularCoordinates(string path, out TabularCoordinateParseResult result)
    {
        result = new TabularCoordinateParseResult(false, null);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var reader = new StreamReader(path);
            var header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header))
            {
                return true;
            }

            var separator = header.Contains('\t') ? '\t' : ',';
            var headers = header.Split(separator).Select(item => item.Trim()).ToArray();
            var eastingIndex = Array.FindIndex(headers, headerValue => headerValue.Contains("east", StringComparison.OrdinalIgnoreCase));
            var northingIndex = Array.FindIndex(headers, headerValue => headerValue.Contains("north", StringComparison.OrdinalIgnoreCase));
            var hasColumns = eastingIndex >= 0 && northingIndex >= 0;

            (double Easting, double Northing)? sample = null;
            if (hasColumns)
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var cells = line.Split(separator);
                    if (cells.Length <= Math.Max(eastingIndex, northingIndex))
                    {
                        continue;
                    }

                    if (double.TryParse(cells[eastingIndex], out var easting)
                        && double.TryParse(cells[northingIndex], out var northing))
                    {
                        sample = (easting, northing);
                        break;
                    }
                }
            }

            result = new TabularCoordinateParseResult(hasColumns, sample);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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

    private sealed record TabularCoordinateParseResult(
        bool HasCoordinateColumns,
        (double Easting, double Northing)? SampleCoordinate);
}
