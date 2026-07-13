namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public sealed record PortalAuthResult(
    bool Success,
    string? Token,
    string Source,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? ValidatedAtUtc,
    string? ErrorMessage,
    IReadOnlyList<string>? AttemptedSources = null)
{
    public static PortalAuthResult Succeeded(
        string token,
        string source,
        DateTimeOffset? expiresAtUtc = null,
        DateTimeOffset? validatedAtUtc = null,
        IReadOnlyList<string>? attemptedSources = null)
    {
        return new PortalAuthResult(
            true,
            token,
            source,
            expiresAtUtc,
            validatedAtUtc ?? DateTimeOffset.UtcNow,
            null,
            attemptedSources ?? new[] { source });
    }

    public static PortalAuthResult Failed(
        string source,
        string errorMessage,
        IReadOnlyList<string>? attemptedSources = null)
    {
        return new PortalAuthResult(
            false,
            null,
            source,
            null,
            DateTimeOffset.UtcNow,
            errorMessage,
            attemptedSources ?? new[] { source });
    }

    public override string ToString()
    {
        var attempted = AttemptedSources is { Count: > 0 }
            ? string.Join(", ", AttemptedSources)
            : Source;
        return Success
            ? $"Portal auth succeeded via {Source}; attempted sources: {attempted}."
            : $"Portal auth failed via {Source}; attempted sources: {attempted}; {ErrorMessage}";
    }
}
