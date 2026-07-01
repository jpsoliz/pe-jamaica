using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.Disposition;

public sealed class ComputeReviewDispositionPersistenceService
{
    public const string DispositionArtifactFileName = "compute_review_disposition.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string GetDispositionPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, DispositionArtifactFileName);
    }

    public string Save(CaseFolderLayout layout, ComputeReviewDispositionDocument document)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var path = GetDispositionPath(layout);
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        return path;
    }

    public ComputeReviewDispositionDocument? Load(CaseFolderLayout layout)
    {
        var path = GetDispositionPath(layout);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ComputeReviewDispositionDocument>(File.ReadAllText(path), JsonOptions)
            : null;
    }
}
