using ParcelWorkflowAddIn.Enterprise.PortalAuth;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public interface ICompareWorkingGeometryService
{
    CompareWorkingGeometryLoadPlan BuildLoadPlan(SelectedInnolaTransaction transaction);

    Task<CompareWorkingGeometryLoadResult> LoadAsync(
        SelectedInnolaTransaction transaction,
        CancellationToken cancellationToken = default);
}

public interface ICompareMapIntegrationService
{
    Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
        CompareWorkingGeometryLoadPlan plan,
        CancellationToken cancellationToken = default);
}

public sealed class CompareWorkingGeometryService : ICompareWorkingGeometryService
{
    private const string TransactionNumberField = "transaction_number";
    private const string TransactionIdField = "transaction_id";

    private readonly Func<InnolaTransactionSettings> getSettings;
    private readonly ICompareMapIntegrationService? mapIntegrationService;

    public CompareWorkingGeometryService(
        Func<InnolaTransactionSettings>? getSettings = null,
        ICompareMapIntegrationService? mapIntegrationService = null)
    {
        this.getSettings = getSettings ?? InnolaTransactionSettings.Load;
        this.mapIntegrationService = mapIntegrationService;
    }

    public CompareWorkingGeometryLoadPlan BuildLoadPlan(SelectedInnolaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var settings = getSettings().EnterpriseWorkingReview;
        if (!settings.Enabled)
        {
            return CompareWorkingGeometryLoadPlan.Invalid(
                transaction,
                "Enterprise working review is disabled for Compare geometry loading.");
        }

        if (!settings.HasRequiredTargets)
        {
            return CompareWorkingGeometryLoadPlan.Invalid(
                transaction,
                "Enterprise working review layer targets are incomplete. Configure points, lines, polygons, and transaction_scope_field.");
        }

        var scopeField = settings.TransactionScopeField.Trim();
        if (!IsSafeFieldName(scopeField))
        {
            return CompareWorkingGeometryLoadPlan.Invalid(
                transaction,
                $"Enterprise working review transaction scope field '{scopeField}' is not safe for a definition query.");
        }

        var scopeValue = ResolveScopeValue(transaction, scopeField);
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return CompareWorkingGeometryLoadPlan.Invalid(
                transaction,
                $"Transaction value for scope field '{scopeField}' is not available.");
        }

        var definitionQuery = BuildDefinitionQuery(scopeField, scopeValue);
        var layers = new[]
        {
            new CompareWorkingLayerRequest(CompareWorkingLayerRole.Polygons, settings.Layers.Polygons!, definitionQuery, true),
            new CompareWorkingLayerRequest(CompareWorkingLayerRole.Lines, settings.Layers.Lines!, definitionQuery, true),
            new CompareWorkingLayerRequest(CompareWorkingLayerRole.Points, settings.Layers.Points!, definitionQuery, true)
        };

        return new CompareWorkingGeometryLoadPlan(
            true,
            transaction.TransactionId,
            transaction.TransactionNumber,
            ResolvePortalUrl(settings.ServiceRoot),
            scopeField,
            scopeValue,
            definitionQuery,
            layers,
            null);
    }

    public async Task<CompareWorkingGeometryLoadResult> LoadAsync(
        SelectedInnolaTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var plan = BuildLoadPlan(transaction);
        if (!plan.IsValid)
        {
            return CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.SettingsInvalid,
                plan.InvalidReason ?? "Compare geometry settings are invalid.",
                plan);
        }

        if (mapIntegrationService is null)
        {
            return CompareWorkingGeometryLoadResult.Ready(
                "Compare working geometry load plan is ready.",
                plan,
                null);
        }

        CompareMapIntegrationResult mapResult;
        try
        {
            mapResult = await mapIntegrationService
                .AddTransactionGeometryToActiveMapAsync(plan, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            mapResult = CompareMapIntegrationResult.Failed($"Compare working geometry could not be loaded: {exception.Message}");
        }

        return mapResult.Status switch
        {
            CompareMapIntegrationStatus.MapUnavailable => CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.MapUnavailable,
                mapResult.Message,
                plan,
                mapResult,
                retryable: true),
            CompareMapIntegrationStatus.NoPolygons => CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.NoPolygons,
                mapResult.Message,
                plan,
                mapResult),
            CompareMapIntegrationStatus.Failed => CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.MapLoadFailed,
                mapResult.Message,
                plan,
                mapResult,
                retryable: true),
            _ when mapResult.PolygonFeatureCount == 0 => CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.NoPolygons,
                $"No working_review polygons were found for {plan.ScopeField} '{plan.ScopeValue}'.",
                plan,
                mapResult),
            _ => CompareWorkingGeometryLoadResult.Ready(mapResult.Message, plan, mapResult)
        };
    }

    public static string BuildDefinitionQuery(string scopeField, string scopeValue)
    {
        if (!IsSafeFieldName(scopeField))
        {
            throw new ArgumentException("Scope field is not safe for a definition query.", nameof(scopeField));
        }

        return $"{scopeField.Trim()} = '{scopeValue.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    public static string ResolveScopeValue(SelectedInnolaTransaction transaction, string scopeField)
    {
        if (scopeField.Equals(TransactionIdField, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeTransactionNumber(transaction.TransactionNumber);
        }

        if (scopeField.Equals(TransactionNumberField, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeTransactionNumber(transaction.TransactionNumber);
        }

        return transaction.TransactionNumber.Trim();
    }

    public static string NormalizeTransactionNumber(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("TR", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..]
            : trimmed;
    }

    public static string? ResolvePortalUrl(string? serviceRoot)
    {
        if (string.IsNullOrWhiteSpace(serviceRoot))
        {
            return null;
        }

        if (!Uri.TryCreate(serviceRoot.Trim(), UriKind.Absolute, out var uri))
        {
            return serviceRoot.Trim();
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.StartsWith("/portal", StringComparison.OrdinalIgnoreCase))
        {
            return uri.ToString().TrimEnd('/');
        }

        var serverIndex = path.IndexOf("/server/rest", StringComparison.OrdinalIgnoreCase);
        if (serverIndex >= 0)
        {
            var portalPath = string.Concat(path.AsSpan(0, serverIndex), "/portal");
            return new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port, portalPath).Uri.ToString().TrimEnd('/');
        }

        return uri.ToString().TrimEnd('/');
    }

    private static bool IsSafeFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!char.IsLetter(trimmed[0]) && trimmed[0] != '_')
        {
            return false;
        }

        return trimmed.All(character => char.IsLetterOrDigit(character) || character == '_');
    }
}

public sealed record CompareWorkingGeometryLoadPlan(
    bool IsValid,
    string TransactionId,
    string TransactionNumber,
    string? PortalUrl,
    string ScopeField,
    string ScopeValue,
    string DefinitionQuery,
    IReadOnlyList<CompareWorkingLayerRequest> Layers,
    string? InvalidReason)
{
    public static CompareWorkingGeometryLoadPlan Invalid(SelectedInnolaTransaction transaction, string reason)
    {
        return new CompareWorkingGeometryLoadPlan(
            false,
            transaction.TransactionId,
            transaction.TransactionNumber,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<CompareWorkingLayerRequest>(),
            reason);
    }
}

public sealed record CompareWorkingLayerRequest(
    CompareWorkingLayerRole Role,
    string LayerUrl,
    string DefinitionQuery,
    bool Required);

public enum CompareWorkingLayerRole
{
    Polygons,
    Lines,
    Points
}

public sealed record CompareWorkingGeometryLoadResult(
    CompareGeometryLoadStatus Status,
    string Message,
    CompareWorkingGeometryLoadPlan? Plan,
    CompareMapIntegrationResult? MapResult,
    bool BlocksApproval,
    bool Retryable)
{
    public bool Success => Status == CompareGeometryLoadStatus.Ready;

    public static CompareWorkingGeometryLoadResult Ready(
        string message,
        CompareWorkingGeometryLoadPlan plan,
        CompareMapIntegrationResult? mapResult)
    {
        return new CompareWorkingGeometryLoadResult(
            CompareGeometryLoadStatus.Ready,
            message,
            plan,
            mapResult,
            false,
            false);
    }

    public static CompareWorkingGeometryLoadResult Blocked(
        CompareGeometryLoadStatus status,
        string message,
        CompareWorkingGeometryLoadPlan? plan,
        CompareMapIntegrationResult? mapResult = null,
        bool retryable = false)
    {
        return new CompareWorkingGeometryLoadResult(
            status,
            message,
            plan,
            mapResult,
            true,
            retryable);
    }
}

public enum CompareGeometryLoadStatus
{
    Ready,
    SettingsInvalid,
    MapUnavailable,
    NoPolygons,
    MapLoadFailed
}

public sealed record CompareMapIntegrationResult(
    CompareMapIntegrationStatus Status,
    string Message,
    IReadOnlyList<string> LoadedLayerUrls,
    string? GroupLayerName,
    int? PolygonFeatureCount = null)
{
    public bool Success => Status == CompareMapIntegrationStatus.Loaded;

    public static CompareMapIntegrationResult Loaded(
        string message,
        IReadOnlyList<string> loadedLayerUrls,
        string? groupLayerName,
        int? polygonFeatureCount = null)
    {
        return new CompareMapIntegrationResult(
            CompareMapIntegrationStatus.Loaded,
            message,
            loadedLayerUrls,
            groupLayerName,
            polygonFeatureCount);
    }

    public static CompareMapIntegrationResult MapUnavailable(string message)
    {
        return new CompareMapIntegrationResult(
            CompareMapIntegrationStatus.MapUnavailable,
            message,
            Array.Empty<string>(),
            null);
    }

    public static CompareMapIntegrationResult NoPolygons(string message, string? groupLayerName = null)
    {
        return new CompareMapIntegrationResult(
            CompareMapIntegrationStatus.NoPolygons,
            message,
            Array.Empty<string>(),
            groupLayerName,
            0);
    }

    public static CompareMapIntegrationResult Failed(string message)
    {
        return new CompareMapIntegrationResult(
            CompareMapIntegrationStatus.Failed,
            message,
            Array.Empty<string>(),
            null);
    }
}

public enum CompareMapIntegrationStatus
{
    Loaded,
    MapUnavailable,
    NoPolygons,
    Failed
}

public sealed record CompareWorkspaceLoadState(
    CompareDocumentLoadState Documents,
    CompareWorkingGeometryLoadResult Geometry)
{
    public bool CanApproveCompare => Documents.Success && Geometry.Success && !Geometry.BlocksApproval;
}

public sealed record CompareDocumentLoadState(
    bool Success,
    string Message,
    string? CaseFolderPath,
    bool Retryable)
{
    public static CompareDocumentLoadState Loaded(string message, string? caseFolderPath)
    {
        return new CompareDocumentLoadState(true, message, caseFolderPath, false);
    }

    public static CompareDocumentLoadState Failed(string message, bool retryable = true)
    {
        return new CompareDocumentLoadState(false, message, null, retryable);
    }
}

public sealed class CompareWorkspaceLoadService
{
    private readonly InnolaSessionManager sessionManager;
    private readonly InnolaTransactionLoadService transactionLoadService;
    private readonly ICompareWorkingGeometryService geometryService;

    public CompareWorkspaceLoadService(
        InnolaSessionManager sessionManager,
        InnolaTransactionLoadService transactionLoadService,
        ICompareWorkingGeometryService geometryService)
    {
        this.sessionManager = sessionManager;
        this.transactionLoadService = transactionLoadService;
        this.geometryService = geometryService;
    }

    public async Task<CompareWorkspaceLoadState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (sessionManager.SelectedTransaction is null)
        {
            return new CompareWorkspaceLoadState(
                CompareDocumentLoadState.Failed("Select a transaction before opening Compare."),
                CompareWorkingGeometryLoadResult.Blocked(
                    CompareGeometryLoadStatus.SettingsInvalid,
                    "Select a transaction before loading Compare geometry.",
                    null));
        }

        var selectedTransaction = sessionManager.SelectedTransaction;
        var documentResult = await transactionLoadService.LoadSelectedTransactionAsync(cancellationToken).ConfigureAwait(false);
        var documentState = documentResult.Success
            ? CompareDocumentLoadState.Loaded(
                documentResult.StatusMessage ?? $"Loaded transaction {selectedTransaction.TransactionNumber} into Case Folder.",
                documentResult.Layout?.RootDirectory)
            : CompareDocumentLoadState.Failed(documentResult.ErrorMessage ?? "Could not load Compare documents.");

        var geometryState = await geometryService.LoadAsync(selectedTransaction, cancellationToken).ConfigureAwait(false);
        return new CompareWorkspaceLoadState(documentState, geometryState);
    }

    public async Task<CompareWorkingGeometryLoadResult> LoadGeometryAsync(CancellationToken cancellationToken = default)
    {
        return sessionManager.SelectedTransaction is null
            ? CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.SettingsInvalid,
                "Select a transaction before loading Compare geometry.",
                null)
            : await geometryService.LoadAsync(sessionManager.SelectedTransaction, cancellationToken).ConfigureAwait(false);
    }
}
