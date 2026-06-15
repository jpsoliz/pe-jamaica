using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Output;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

public sealed class SpatialReviewApprovalPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly OutputSummaryPersistenceService outputSummaryPersistenceService = new();

    public string ApprovalArtifactFileName => "spatial_review_approval.json";

    public string GetApprovalPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, ApprovalArtifactFileName);
    }

    public SpatialReviewApprovalDocument Save(CaseFolderLayout layout, OutputSummaryDocument summary, string? operatorId)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var document = new SpatialReviewApprovalDocument(
            "1.0.0",
            summary.TransactionId,
            DateTimeOffset.UtcNow.ToString("O"),
            operatorId,
            summary.CreatedAt,
            summary.SourceManifestHash,
            ComputeArtifactVersion(layout, summary));
        File.WriteAllText(GetApprovalPath(layout), JsonSerializer.Serialize(document, JsonOptions));
        return document;
    }

    public SpatialReviewApprovalDocument? Load(CaseFolderLayout layout)
    {
        var approvalPath = GetApprovalPath(layout);
        if (!File.Exists(approvalPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SpatialReviewApprovalDocument>(File.ReadAllText(approvalPath), JsonOptions);
    }

    public void Delete(CaseFolderLayout layout)
    {
        var approvalPath = GetApprovalPath(layout);
        if (!File.Exists(approvalPath))
        {
            return;
        }

        File.Delete(approvalPath);
    }

    public SpatialReviewApprovalValidationResult ValidateCurrent(CaseFolderLayout layout, OutputSummaryDocument? summary)
    {
        var approval = Load(layout);
        if (approval is null)
        {
            return SpatialReviewApprovalValidationResult.Invalid("Spatial review approval is missing. Review the output layers and approve spatial review again.");
        }

        if (summary is null)
        {
            return SpatialReviewApprovalValidationResult.Invalid("Spatial review approval could not be verified because the output summary is unavailable.");
        }

        if (!string.Equals(approval.OutputSourceManifestHash, summary.SourceManifestHash, StringComparison.OrdinalIgnoreCase))
        {
            return SpatialReviewApprovalValidationResult.Invalid("Spatial review approval no longer matches the current output package. Review the output layers and approve spatial review again.");
        }

        var currentVersion = ComputeArtifactVersion(layout, summary);
        if (!string.Equals(approval.OutputArtifactVersion, currentVersion, StringComparison.Ordinal))
        {
            return SpatialReviewApprovalValidationResult.Invalid("Spatial review approval was invalidated because the output geometry changed after approval. Review the output layers and approve spatial review again.");
        }

        return SpatialReviewApprovalValidationResult.Current(approval);
    }

    public string ComputeArtifactVersion(CaseFolderLayout layout, OutputSummaryDocument summary)
    {
        var artifactPaths = outputSummaryPersistenceService.GetArtifactPaths(layout, summary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (artifactPaths.Length == 0)
        {
            return "no-artifacts";
        }

        var versions = artifactPaths
            .Select(ComputePathVersion)
            .ToArray();
        return string.Join("||", versions);
    }

    private static string ComputePathVersion(string path)
    {
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            return $"file:{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }

        if (!Directory.Exists(path))
        {
            return $"missing:{path}";
        }

        var directoryInfo = new DirectoryInfo(path);
        long latestWriteTicks = directoryInfo.LastWriteTimeUtc.Ticks;
        long fileCount = 0;
        long directoryCount = 0;
        foreach (var childDirectory in directoryInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            directoryCount++;
            latestWriteTicks = Math.Max(latestWriteTicks, childDirectory.LastWriteTimeUtc.Ticks);
        }

        foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            fileCount++;
            latestWriteTicks = Math.Max(latestWriteTicks, file.LastWriteTimeUtc.Ticks);
        }

        return $"dir:{directoryInfo.FullName}|files:{fileCount}|dirs:{directoryCount}|last:{latestWriteTicks}";
    }
}

public sealed record SpatialReviewApprovalDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("approved_at")] string ApprovedAt,
    [property: JsonPropertyName("approved_by")] string? ApprovedBy,
    [property: JsonPropertyName("output_created_at")] string OutputCreatedAt,
    [property: JsonPropertyName("output_source_manifest_hash")] string OutputSourceManifestHash,
    [property: JsonPropertyName("output_artifact_version")] string OutputArtifactVersion);

public sealed record SpatialReviewApprovalValidationResult(
    bool IsCurrent,
    string? ErrorMessage,
    SpatialReviewApprovalDocument? Approval)
{
    public static SpatialReviewApprovalValidationResult Current(SpatialReviewApprovalDocument approval)
    {
        return new SpatialReviewApprovalValidationResult(true, null, approval);
    }

    public static SpatialReviewApprovalValidationResult Invalid(string errorMessage)
    {
        return new SpatialReviewApprovalValidationResult(false, errorMessage, null);
    }
}
