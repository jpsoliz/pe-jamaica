namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public sealed class CompositePortalAuthProvider : IPortalAuthProvider
{
    private readonly IReadOnlyList<IPortalAuthProvider> providers;

    public CompositePortalAuthProvider(IEnumerable<IPortalAuthProvider> providers)
    {
        this.providers = providers?.ToArray() ?? Array.Empty<IPortalAuthProvider>();
    }

    public static CompositePortalAuthProvider CreateDefault()
    {
        return new CompositePortalAuthProvider(new IPortalAuthProvider[]
        {
            new ArcGisProPortalAuthProvider(),
            new EnvironmentPortalAuthProvider()
        });
    }

    public async Task<PortalAuthResult> GetTokenAsync(
        PortalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var attemptedSources = new List<string>();
        var errors = new List<string>();
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await provider.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.AttemptedSources is { Count: > 0 })
            {
                attemptedSources.AddRange(result.AttemptedSources);
            }
            else
            {
                attemptedSources.Add(result.Source);
            }

            if (result.Success && !string.IsNullOrWhiteSpace(result.Token))
            {
                return result with
                {
                    AttemptedSources = attemptedSources
                };
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                errors.Add($"{result.Source}: {result.ErrorMessage}");
            }
        }

        var portalText = string.IsNullOrWhiteSpace(request.PortalUrl)
            ? "the configured ArcGIS portal"
            : request.PortalUrl;
        var operationText = string.IsNullOrWhiteSpace(request.Operation)
            ? "Enterprise operation"
            : request.Operation;

        return PortalAuthResult.Failed(
            "composite",
            $"{operationText} could not authenticate to {portalText}. Attempted sources: {string.Join(", ", attemptedSources)}. {string.Join(" ", errors)}",
            attemptedSources);
    }
}
