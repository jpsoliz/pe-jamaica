namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public sealed class EnvironmentPortalAuthProvider : IPortalAuthProvider
{
    public const string DefaultTokenVariableName = "ARCGIS_PORTAL_TOKEN";
    public const string SourceName = "environment";

    private readonly string tokenVariableName;
    private readonly Func<string, EnvironmentVariableTarget, string?> getEnvironmentVariable;

    public EnvironmentPortalAuthProvider()
        : this(DefaultTokenVariableName, Environment.GetEnvironmentVariable)
    {
    }

    internal EnvironmentPortalAuthProvider(
        string tokenVariableName,
        Func<string, EnvironmentVariableTarget, string?> getEnvironmentVariable)
    {
        this.tokenVariableName = string.IsNullOrWhiteSpace(tokenVariableName)
            ? DefaultTokenVariableName
            : tokenVariableName;
        this.getEnvironmentVariable = getEnvironmentVariable;
    }

    public Task<PortalAuthResult> GetTokenAsync(
        PortalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attemptedSources = new[]
        {
            $"{tokenVariableName}:User",
            $"{tokenVariableName}:Process",
            $"{tokenVariableName}:Machine"
        };

        var truncatedSources = new List<string>();
        foreach (var candidate in EnumerateCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate.Token))
            {
                continue;
            }

            if (LooksTruncated(candidate.Token))
            {
                truncatedSources.Add(candidate.Source);
                continue;
            }

            return Task.FromResult(PortalAuthResult.Succeeded(
                candidate.Token,
                SourceName,
                attemptedSources: attemptedSources));
        }

        if (truncatedSources.Count > 0)
        {
            return Task.FromResult(PortalAuthResult.Failed(
                SourceName,
                $"The portal token from {tokenVariableName} appears to be truncated in {string.Join(", ", truncatedSources)}. Copy the complete token value without trailing ellipsis, or clear the stale truncated value.",
                attemptedSources));
        }

        return Task.FromResult(PortalAuthResult.Failed(
            SourceName,
            $"No portal token was found in {tokenVariableName}.",
            attemptedSources));

        IEnumerable<(string Source, string? Token)> EnumerateCandidates()
        {
            yield return ($"{tokenVariableName}:User", getEnvironmentVariable(tokenVariableName, EnvironmentVariableTarget.User));
            yield return ($"{tokenVariableName}:Process", getEnvironmentVariable(tokenVariableName, EnvironmentVariableTarget.Process));
            yield return ($"{tokenVariableName}:Machine", getEnvironmentVariable(tokenVariableName, EnvironmentVariableTarget.Machine));
        }
    }

    private static bool LooksTruncated(string token)
    {
        return token.TrimEnd().EndsWith("..", StringComparison.Ordinal);
    }

}
