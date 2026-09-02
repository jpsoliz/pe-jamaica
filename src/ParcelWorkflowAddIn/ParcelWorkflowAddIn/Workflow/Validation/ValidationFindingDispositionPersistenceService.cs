using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public sealed class ValidationFindingDispositionPersistenceService
{
    public const string DispositionArtifactFileName = "validation_finding_dispositions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string GetDispositionPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, DispositionArtifactFileName);
    }

    public ValidationFindingDispositionDocument? Load(CaseFolderLayout layout)
    {
        var path = GetDispositionPath(layout);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ValidationFindingDispositionDocument>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public string Save(CaseFolderLayout layout, ValidationFindingDispositionDocument document)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var path = GetDispositionPath(layout);
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        return path;
    }

    public ValidationFindingDispositionDocument Upsert(
        CaseFolderLayout layout,
        string transactionId,
        ValidationFindingDispositionItem item)
    {
        var existing = Load(layout);
        var items = (existing?.Items ?? Array.Empty<ValidationFindingDispositionItem>())
            .Where(current => !string.Equals(current.FindingKey, item.FindingKey, StringComparison.OrdinalIgnoreCase))
            .Append(item)
            .OrderBy(current => current.RuleId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(current => current.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var document = new ValidationFindingDispositionDocument(
            "1.0.0",
            string.IsNullOrWhiteSpace(existing?.TransactionId) ? transactionId : existing.TransactionId,
            DateTimeOffset.UtcNow.ToString("O"),
            items);
        Save(layout, document);
        return document;
    }
}
