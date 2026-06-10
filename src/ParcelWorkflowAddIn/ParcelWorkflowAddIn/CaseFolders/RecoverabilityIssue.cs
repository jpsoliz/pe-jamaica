namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record RecoverabilityIssue(
    string Code,
    string Severity,
    string Message,
    string? Path,
    bool BlocksReopen);
