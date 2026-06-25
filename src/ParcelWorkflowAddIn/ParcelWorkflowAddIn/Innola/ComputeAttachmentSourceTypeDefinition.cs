namespace ParcelWorkflowAddIn.Innola;

public sealed record ComputeAttachmentSourceTypeDefinition(
    string SourceType,
    string WorkflowRole,
    string DisplayName,
    bool Required,
    bool InternalOnly,
    IReadOnlyList<string> Extensions)
{
    public bool SupportsExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var normalized = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";

        return Extensions.Any(candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
