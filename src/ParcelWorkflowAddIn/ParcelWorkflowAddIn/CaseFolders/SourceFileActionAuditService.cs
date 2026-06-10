using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class SourceFileActionAuditService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> getUtcNow;

    public SourceFileActionAuditService()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public SourceFileActionAuditService(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow;
    }

    public void Record(
        CaseFolderLayout layout,
        string transactionId,
        SourceFileCopyResult sourceFile,
        SourceFileActionResult result,
        string? operatorId)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(layout.WorkingDirectory);
            var auditPath = GetAuditPath(layout);
            var existing = ReadExisting(auditPath, transactionId);
            var events = existing.Events.ToList();
            events.Add(new SourceFileActionAuditEvent(
                getUtcNow().UtcDateTime.ToString("O"),
                operatorId,
                result.Action.ToContractValue(),
                sourceFile.FileName,
                result.Path ?? sourceFile.CopiedPath,
                result.Status,
                result.Message));

            var updated = existing with { Events = events };
            File.WriteAllText(auditPath, JsonSerializer.Serialize(updated, Options));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException)
        {
            // Source file actions must remain non-blocking even when bounded audit append fails.
        }
    }

    public static string GetAuditPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, "source_action_audit.json");
    }

    private static SourceFileActionAuditDocument ReadExisting(string auditPath, string transactionId)
    {
        if (!File.Exists(auditPath))
        {
            return new SourceFileActionAuditDocument("1.0.0", transactionId, Array.Empty<SourceFileActionAuditEvent>());
        }

        return JsonSerializer.Deserialize<SourceFileActionAuditDocument>(File.ReadAllText(auditPath), Options)
            ?? new SourceFileActionAuditDocument("1.0.0", transactionId, Array.Empty<SourceFileActionAuditEvent>());
    }
}
