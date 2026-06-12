using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using System.IO;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public sealed class ValidationSummaryPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string ValidationArtifactFileName => "validation_summary.json";

    public ValidationSummaryDocument? Load(CaseFolderLayout layout)
    {
        var path = Path.Combine(layout.WorkingDirectory, ValidationArtifactFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ValidationSummaryDocument>(File.ReadAllText(path), JsonOptions);
    }

    public void Save(CaseFolderLayout layout, ValidationSummaryDocument document)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var path = Path.Combine(layout.WorkingDirectory, ValidationArtifactFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
    }

    public bool IsBlocked(ValidationSummaryDocument? document)
    {
        return string.Equals(document?.Payload.Status, "blocked", StringComparison.OrdinalIgnoreCase);
    }
}
