using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

public sealed class SpatialOverlapReviewPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public const string ComputeArtifactFileName = "compute_overlap_review.json";
    public const string CompareArtifactFileName = "compare_overlap_review.json";

    public SpatialOverlapReviewDocument? Load(CaseFolderLayout layout, string scope)
    {
        var path = GetArtifactPath(layout, scope);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SpatialOverlapReviewDocument>(File.ReadAllText(path), JsonOptions);
    }

    public string Save(CaseFolderLayout layout, SpatialOverlapReviewDocument document)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var path = GetArtifactPath(layout, document.Scope);
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        return path;
    }

    public string GetArtifactPath(CaseFolderLayout layout, string scope)
    {
        var fileName = string.Equals(scope, SpatialOverlapReviewScopes.Compare, StringComparison.OrdinalIgnoreCase)
            ? CompareArtifactFileName
            : ComputeArtifactFileName;
        return Path.Combine(layout.WorkingDirectory, fileName);
    }
}
