using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class WorkflowLifecycleAuditService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> getUtcNow;

    public WorkflowLifecycleAuditService()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public WorkflowLifecycleAuditService(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow;
    }

    public void Record(
        CaseFolderLayout layout,
        string transactionId,
        string action,
        string status,
        string? operatorId,
        string? message,
        string? taskId,
        string? transactionNumber,
        string? errorCategory)
    {
        try
        {
            Directory.CreateDirectory(layout.WorkingDirectory);
            var auditPath = GetAuditPath(layout);
            var existing = ReadExisting(auditPath, transactionId);
            var events = existing.Events.ToList();
            events.Add(new WorkflowLifecycleAuditEvent(
                getUtcNow().UtcDateTime.ToString("O"),
                operatorId,
                action,
                status,
                Sanitize(message),
                taskId,
                transactionNumber,
                Sanitize(errorCategory)));

            File.WriteAllText(auditPath, JsonSerializer.Serialize(existing with { Events = events }, Options));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException)
        {
            // Lifecycle audit must not block the workflow; manifest state remains authoritative.
        }
    }

    public static string GetAuditPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, "workflow_lifecycle_audit.json");
    }

    private static WorkflowLifecycleAuditDocument ReadExisting(string auditPath, string transactionId)
    {
        if (!File.Exists(auditPath))
        {
            return new WorkflowLifecycleAuditDocument("1.0.0", transactionId, Array.Empty<WorkflowLifecycleAuditEvent>());
        }

        return JsonSerializer.Deserialize<WorkflowLifecycleAuditDocument>(File.ReadAllText(auditPath), Options)
            ?? new WorkflowLifecycleAuditDocument("1.0.0", transactionId, Array.Empty<WorkflowLifecycleAuditEvent>());
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("access", StringComparison.OrdinalIgnoreCase)
            || value.Contains("{", StringComparison.Ordinal)
            || value.Contains("}", StringComparison.Ordinal))
        {
            return "Lifecycle action failed. Try again.";
        }

        return value;
    }
}
