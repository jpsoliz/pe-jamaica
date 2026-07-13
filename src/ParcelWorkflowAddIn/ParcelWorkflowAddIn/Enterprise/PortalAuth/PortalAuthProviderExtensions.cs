namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public static class PortalAuthProviderExtensions
{
    public static async Task<string> GetRequiredTokenAsync(
        this IPortalAuthProvider provider,
        PortalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await provider.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Success && !string.IsNullOrWhiteSpace(result.Token))
        {
            return result.Token;
        }

        var attemptedSources = result.AttemptedSources is { Count: > 0 }
            ? string.Join(", ", result.AttemptedSources)
            : result.Source;
        var message = result.ErrorMessage ?? "ArcGIS Enterprise authentication failed.";
        throw new InvalidOperationException(
            $"ArcGIS Enterprise authentication failed for operation '{request.Operation}' against portal '{request.PortalUrl}'. " +
            $"Auth source: {result.Source}. Attempted sources: {attemptedSources}. {message}");
    }
}
