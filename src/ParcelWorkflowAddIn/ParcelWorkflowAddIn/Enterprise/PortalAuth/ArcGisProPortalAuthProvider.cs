using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace ParcelWorkflowAddIn.Enterprise.PortalAuth;

public sealed class ArcGisProPortalAuthProvider : IPortalAuthProvider
{
    public const string SourceName = "arcgis_pro_session";
    private readonly Func<Type?> portalManagerTypeResolver;

    public ArcGisProPortalAuthProvider()
        : this(() => Type.GetType("ArcGIS.Desktop.Core.ArcGISPortalManager, ArcGIS.Desktop.Core", throwOnError: false))
    {
    }

    internal ArcGisProPortalAuthProvider(Func<Type?> portalManagerTypeResolver)
    {
        this.portalManagerTypeResolver = portalManagerTypeResolver;
    }

    public async Task<PortalAuthResult> GetTokenAsync(
        PortalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var managerType = portalManagerTypeResolver();
            if (managerType is null)
            {
                return PortalAuthResult.Failed(
                    SourceName,
                    "ArcGIS Pro portal manager was not available in this process.");
            }

            var manager = ResolveCurrentManager(managerType);
            if (manager is null)
            {
                return PortalAuthResult.Failed(
                    SourceName,
                    "ArcGIS Pro portal manager did not expose a current instance.");
            }

            var portal = await ResolveActivePortalAsync(manager, cancellationToken).ConfigureAwait(false);
            if (portal is null)
            {
                return PortalAuthResult.Failed(
                    SourceName,
                    "No active ArcGIS Pro portal session was found.");
            }

            var portalUri = ResolvePortalUri(portal);
            if (!PortalMatchesRequest(portalUri, request.PortalUrl))
            {
                return PortalAuthResult.Failed(
                    SourceName,
                    $"Active ArcGIS Pro portal '{portalUri?.ToString() ?? "(unknown)"}' does not match configured portal '{request.PortalUrl}'.");
            }

            var token = await ResolvePortalTokenAsync(portal, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return PortalAuthResult.Succeeded(token, SourceName);
            }

            var queuedRetry = await TryResolvePortalTokenOnQueuedTaskAsync(managerType, request, cancellationToken).ConfigureAwait(false);
            if (queuedRetry.Success)
            {
                return queuedRetry;
            }

            return PortalAuthResult.Failed(
                SourceName,
                $"Active ArcGIS Pro portal session did not return a token. {ResolvePortalSessionDiagnostic(portal)} Queued task retry: {queuedRetry.ErrorMessage}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PortalAuthResult.Failed(
                SourceName,
                $"ArcGIS Pro portal session token retrieval failed: {exception.GetType().Name}.");
        }
    }

    private static object? ResolveCurrentManager(Type managerType)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;
        foreach (var propertyName in new[] { "Current", "Instance" })
        {
            var property = managerType.GetProperty(propertyName, flags);
            var value = property?.GetValue(null);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<object?> ResolveActivePortalAsync(object manager, CancellationToken cancellationToken)
    {
        foreach (var methodName in new[] { "GetActivePortal", "GetActivePortalAsync" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var method = manager.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            if (method is null)
            {
                continue;
            }

            var value = method.Invoke(manager, Array.Empty<object>());
            return await UnwrapPossiblyAsync(value, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<PortalAuthResult> TryResolvePortalTokenOnQueuedTaskAsync(
        Type managerType,
        PortalAuthRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await QueuedTask.Run(
                () => ResolvePortalTokenOnCurrentThread(managerType, request, cancellationToken),
                TaskCreationOptions.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PortalAuthResult.Failed(
                SourceName,
                $"ArcGIS Pro queued task token retry failed: {exception.GetType().Name}.");
        }
    }

    private static PortalAuthResult ResolvePortalTokenOnCurrentThread(
        Type managerType,
        PortalAuthRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = ResolveCurrentManager(managerType);
        if (manager is null)
        {
            return PortalAuthResult.Failed(
                SourceName,
                "ArcGIS Pro portal manager did not expose a current instance during queued task retry.");
        }

        var portal = ResolveActivePortalAsync(manager, cancellationToken).GetAwaiter().GetResult();
        if (portal is null)
        {
            return PortalAuthResult.Failed(
                SourceName,
                "No active ArcGIS Pro portal session was found during queued task retry.");
        }

        var portalUri = ResolvePortalUri(portal);
        if (!PortalMatchesRequest(portalUri, request.PortalUrl))
        {
            return PortalAuthResult.Failed(
                SourceName,
                $"Queued task active ArcGIS Pro portal '{portalUri?.ToString() ?? "(unknown)"}' does not match configured portal '{request.PortalUrl}'.");
        }

        var token = ResolvePortalTokenAsync(portal, cancellationToken).GetAwaiter().GetResult();
        return string.IsNullOrWhiteSpace(token)
            ? PortalAuthResult.Failed(SourceName, $"Queued task active ArcGIS Pro portal session did not return a token. {ResolvePortalSessionDiagnostic(portal)}")
            : PortalAuthResult.Succeeded(token, SourceName);
    }

    private static Uri? ResolvePortalUri(object portal)
    {
        foreach (var propertyName in new[] { "PortalUri", "PortalUrl", "Uri", "Url" })
        {
            var property = portal.GetType().GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var value = property?.GetValue(portal);
            if (value is Uri uri)
            {
                return uri;
            }

            if (value is string text && Uri.TryCreate(text, UriKind.Absolute, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string ResolvePortalSessionDiagnostic(object portal)
    {
        var parts = new List<string>();
        AppendMethodValue(parts, portal, "IsSignedOn", "signed_on");
        AppendMethodValue(parts, portal, "IsActivePortal", "active_portal");
        AppendMethodValue(parts, portal, "IsPortalAvailable", "portal_available");
        AppendMethodValue(parts, portal, "GetSignOnUsername", "username");
        return parts.Count == 0
            ? "Portal session state was not available."
            : $"Portal session state: {string.Join(", ", parts)}.";
    }

    private static void AppendMethodValue(List<string> parts, object target, string methodName, string label)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        if (method is null)
        {
            return;
        }

        try
        {
            var value = method.Invoke(target, Array.Empty<object>());
            parts.Add($"{label}={NormalizeDiagnosticValue(value)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            parts.Add($"{label}=unavailable");
        }
    }

    private static string NormalizeDiagnosticValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            bool flag => flag ? "true" : "false",
            string text when string.IsNullOrWhiteSpace(text) => "(blank)",
            string text => text,
            _ => value.ToString() ?? "(null)"
        };
    }

    private static async Task<string?> ResolvePortalTokenAsync(object portal, CancellationToken cancellationToken)
    {
        foreach (var methodName in new[] { "GetToken", "GetTokenAsync", "GetSignOnToken", "GetSignOnTokenAsync", "GetAuthenticationToken", "GetAuthenticationTokenAsync" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var method = portal.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            if (method is null)
            {
                continue;
            }

            try
            {
                var value = method.Invoke(portal, Array.Empty<object>());
                var unwrapped = await UnwrapPossiblyAsync(value, cancellationToken).ConfigureAwait(false);
                var token = ExtractToken(unwrapped);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    return token;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                continue;
            }
        }

        foreach (var propertyName in new[] { "Token", "AccessToken", "SignOnToken", "AuthenticationToken" })
        {
            var property = portal.GetType().GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var token = ExtractToken(property?.GetValue(portal));
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private static async Task<object?> UnwrapPossiblyAsync(object? value, CancellationToken cancellationToken)
    {
        if (value is Task task)
        {
            await task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }

        return value;
    }

    private static string? ExtractToken(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string token)
        {
            return token;
        }

        foreach (var propertyName in new[] { "Token", "AccessToken", "Value" })
        {
            var property = value.GetType().GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property?.GetValue(value) is string propertyToken)
            {
                return propertyToken;
            }
        }

        return null;
    }

    private static bool PortalMatchesRequest(Uri? activePortalUri, string configuredPortalUrl)
    {
        if (activePortalUri is null || string.IsNullOrWhiteSpace(configuredPortalUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(configuredPortalUrl, UriKind.Absolute, out var configuredUri))
        {
            return true;
        }

        if (!string.Equals(activePortalUri.Host, configuredUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var activePath = NormalizePath(activePortalUri.AbsolutePath);
        var configuredPath = NormalizePath(configuredUri.AbsolutePath);
        if (string.Equals(configuredPath, "/", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(activePath, "/", StringComparison.Ordinal)
            && string.Equals(configuredPath, "/portal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(activePath, configuredPath, StringComparison.OrdinalIgnoreCase)
            || activePath.StartsWith(configuredPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "/" : path.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }
}
