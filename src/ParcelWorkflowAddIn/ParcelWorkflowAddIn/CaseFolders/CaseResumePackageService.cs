using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class CaseResumePackageService
{
    public const string ResumeManifestFileName = "sidwell_resume_manifest.json";

    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly Func<string> getAddInVersion;

    public CaseResumePackageService()
        : this(() => DateTimeOffset.UtcNow, ResolveAddInVersion)
    {
    }

    public CaseResumePackageService(Func<DateTimeOffset> getUtcNow, Func<string> getAddInVersion)
    {
        this.getUtcNow = getUtcNow;
        this.getAddInVersion = getAddInVersion;
    }

    public ResumePackageBuildResult Build(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        string? operatorId,
        bool includeFullOutputArtifacts = false)
    {
        try
        {
            if (!File.Exists(layout.ManifestPath))
            {
                return ResumePackageBuildResult.Failed("Case Folder manifest is missing. Save and Close cannot continue.");
            }

            var manifest = ManifestSerializer.Read(layout.ManifestPath);
            var payload = manifest.Payload;
            var now = getUtcNow().UtcDateTime.ToString("O");
            var tempPackagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{InnolaResumePackageConventions.BuildResumeAttachmentFileName(transaction.TransactionNumber)}");
            var resumeManifest = new ResumePackageManifest(
                "1.0.0",
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                payload.WorkflowState,
                now,
                operatorId,
                getAddInVersion(),
                manifest.RunId,
                manifest.SchemaVersion);

            using (var fileStream = File.Create(tempPackagePath))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                var resumeManifestEntry = archive.CreateEntry(ResumeManifestFileName, CompressionLevel.SmallestSize);
                using (var entryStream = resumeManifestEntry.Open())
                {
                    JsonSerializer.Serialize(entryStream, resumeManifest, ResumeManifestJsonContext.Default.ResumePackageManifest);
                }

                foreach (var filePath in Directory.EnumerateFiles(layout.RootDirectory, "*", SearchOption.AllDirectories))
                {
                    var fullFilePath = Path.GetFullPath(filePath);
                    var relativePath = Path.GetRelativePath(layout.RootDirectory, fullFilePath);
                    if (string.IsNullOrWhiteSpace(relativePath)
                        || relativePath.Equals(ResumeManifestFileName, StringComparison.OrdinalIgnoreCase)
                        || !ShouldIncludeInResumePackage(relativePath, includeFullOutputArtifacts))
                    {
                        continue;
                    }

                    var entryName = relativePath.Replace('\\', '/');
                    archive.CreateEntryFromFile(fullFilePath, entryName, CompressionLevel.SmallestSize);
                }
            }

            var packageInfo = new FileInfo(tempPackagePath);
            Debug.WriteLine(
                $"Innola resume package built. TransactionNumber={transaction.TransactionNumber}; Path={tempPackagePath}; Bytes={packageInfo.Length}.");

            return ResumePackageBuildResult.Succeeded(
                tempPackagePath,
                InnolaResumePackageConventions.BuildResumeAttachmentFileName(transaction.TransactionNumber),
                "application/zip",
                resumeManifest);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException
            or JsonException)
        {
            return ResumePackageBuildResult.Failed($"Resume package could not be created: {exception.Message}");
        }
    }

    public ResumePackageRestoreResult Restore(
        string outputRoot,
        SelectedInnolaTransaction transaction,
        byte[] packageContent)
    {
        if (packageContent.Length == 0)
        {
            return ResumePackageRestoreResult.Failed("Saved resume package is empty.");
        }

        var expectedLayout = CaseFolderLayout.For(outputRoot, transaction.TransactionNumber);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sidwell-resume-restore-{Guid.NewGuid():N}");
        var extractRoot = Path.Combine(tempRoot, "case");

        try
        {
            Directory.CreateDirectory(extractRoot);
            using var archive = new ZipArchive(new MemoryStream(packageContent, writable: false), ZipArchiveMode.Read, leaveOpen: false);
            var resumeManifestEntry = archive.GetEntry(ResumeManifestFileName);
            if (resumeManifestEntry is null)
            {
                return ResumePackageRestoreResult.Failed("Saved resume package manifest is missing.");
            }

            ResumePackageManifest? resumeManifest;
            using (var entryStream = resumeManifestEntry.Open())
            {
                resumeManifest = JsonSerializer.Deserialize(entryStream, ResumeManifestJsonContext.Default.ResumePackageManifest);
            }

            if (resumeManifest is null)
            {
                return ResumePackageRestoreResult.Failed("Saved resume package manifest is unreadable.");
            }

            if (!resumeManifest.TransactionNumber.Equals(transaction.TransactionNumber, StringComparison.OrdinalIgnoreCase)
                || !resumeManifest.TaskId.Equals(transaction.TaskId, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(transaction.TransactionId)
                    && !resumeManifest.TransactionId.Equals(transaction.TransactionId, StringComparison.OrdinalIgnoreCase))
            {
                return ResumePackageRestoreResult.Failed("Saved resume package does not belong to the selected transaction.");
            }

            foreach (var entry in archive.Entries)
            {
                var entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (string.IsNullOrWhiteSpace(entryName)
                    || entryName.Equals(".", StringComparison.Ordinal)
                    || entryName.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var destinationPath = Path.GetFullPath(Path.Combine(extractRoot, entryName));
                var extractRootWithSeparator = Path.GetFullPath(extractRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!destinationPath.StartsWith(extractRootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return ResumePackageRestoreResult.Failed("Saved resume package contains an unsafe path.");
                }

                var directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
            }

            if (!File.Exists(Path.Combine(extractRoot, "manifest.json")))
            {
                return ResumePackageRestoreResult.Failed("Saved resume package did not contain a Case Folder manifest.");
            }

            if (Directory.Exists(expectedLayout.RootDirectory))
            {
                Directory.Delete(expectedLayout.RootDirectory, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(expectedLayout.RootDirectory)!);
            Directory.Move(extractRoot, expectedLayout.RootDirectory);
            return ResumePackageRestoreResult.Succeeded(expectedLayout, resumeManifest);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException)
        {
            return ResumePackageRestoreResult.Failed($"Saved resume package could not be restored: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch (Exception)
            {
                // Best effort cleanup only.
            }
        }
    }

    private static string ResolveAddInVersion()
    {
        return typeof(CaseResumePackageService).Assembly.GetName().Version?.ToString() ?? "dev";
    }

    private static bool ShouldIncludeInResumePackage(string relativePath, bool includeFullOutputArtifacts)
    {
        var normalized = relativePath.Replace('\\', '/');

        if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("source/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("working/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (includeFullOutputArtifacts && normalized.StartsWith("output/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Equals("output/output_summary.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("output/reports/", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Equals("output/logs/process.log", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

public sealed record ResumePackageBuildResult(
    bool Success,
    string? PackagePath,
    string? AttachmentFileName,
    string? ContentType,
    ResumePackageManifest? Manifest,
    string? ErrorMessage)
{
    public static ResumePackageBuildResult Succeeded(
        string packagePath,
        string attachmentFileName,
        string contentType,
        ResumePackageManifest manifest)
    {
        return new ResumePackageBuildResult(true, packagePath, attachmentFileName, contentType, manifest, null);
    }

    public static ResumePackageBuildResult Failed(string errorMessage)
    {
        return new ResumePackageBuildResult(false, null, null, null, null, errorMessage);
    }
}

public sealed record ResumePackageRestoreResult(
    bool Success,
    CaseFolderLayout? Layout,
    ResumePackageManifest? Manifest,
    string? ErrorMessage)
{
    public static ResumePackageRestoreResult Succeeded(CaseFolderLayout layout, ResumePackageManifest manifest)
    {
        return new ResumePackageRestoreResult(true, layout, manifest, null);
    }

    public static ResumePackageRestoreResult Failed(string errorMessage)
    {
        return new ResumePackageRestoreResult(false, null, null, errorMessage);
    }
}

public sealed record ResumePackageManifest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string TaskId,
    [property: JsonPropertyName("workflow_state")] string WorkflowState,
    [property: JsonPropertyName("saved_at")] string SavedAt,
    [property: JsonPropertyName("saved_by")] string? SavedBy,
    [property: JsonPropertyName("add_in_version")] string AddInVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("manifest_schema_version")] string ManifestSchemaVersion);

[JsonSerializable(typeof(ResumePackageManifest))]
internal sealed partial class ResumeManifestJsonContext : JsonSerializerContext
{
}
