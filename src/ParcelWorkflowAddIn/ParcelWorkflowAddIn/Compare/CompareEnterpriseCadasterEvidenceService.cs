using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public interface ICompareEnterpriseCadasterEvidenceService
{
    Task<CompareEnterpriseCadasterEvidenceResult> QueryAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default);
}

public interface ICompareEnterpriseCadasterSpatialQueryExecutor
{
    Task<IReadOnlyList<CompareEnterpriseCadasterEvidenceRecord>> QueryLayerAsync(
        CompareEnterpriseCadasterLayerRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CompareEnterpriseCadasterEvidenceService : ICompareEnterpriseCadasterEvidenceService
{
    private readonly Func<InnolaTransactionSettings> getSettings;
    private readonly ICompareEnterpriseCadasterSpatialQueryExecutor? queryExecutor;
    private readonly Func<DateTimeOffset> getUtcNow;

    public CompareEnterpriseCadasterEvidenceService(
        Func<InnolaTransactionSettings>? getSettings = null,
        ICompareEnterpriseCadasterSpatialQueryExecutor? queryExecutor = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getSettings = getSettings ?? InnolaTransactionSettings.Load;
        this.queryExecutor = queryExecutor;
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public static CompareEnterpriseCadasterQueryPlan BuildQueryPlan(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CompareEnterpriseCadasterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(settings);
        var query = new CompareEnterpriseCadasterEvidenceQuery(
            transaction.TransactionNumber,
            geometryPlan?.ScopeField,
            geometryPlan?.ScopeValue);
        var diagnostics = new List<string>();
        if (!settings.Enabled)
        {
            return CompareEnterpriseCadasterQueryPlan.Invalid(
                query,
                "Enterprise Legal/Fiscal cadaster evidence is disabled.");
        }

        if (geometryPlan is null || !geometryPlan.IsValid || string.IsNullOrWhiteSpace(geometryPlan.ScopeValue))
        {
            return CompareEnterpriseCadasterQueryPlan.Invalid(
                query,
                "Transaction working_review polygon must be loaded before querying Legal/Fiscal cadaster evidence.");
        }

        if (!string.IsNullOrWhiteSpace(settings.Warning))
        {
            diagnostics.Add(settings.Warning);
        }

        var requests = new List<CompareEnterpriseCadasterLayerRequest>();
        AddRequest(settings, settings.Legal, CompareEnterpriseCadasterSourceKind.Legal, geometryPlan, requests, diagnostics);
        AddRequest(settings, settings.Fiscal, CompareEnterpriseCadasterSourceKind.Fiscal, geometryPlan, requests, diagnostics);

        if (requests.Count == 0)
        {
            return CompareEnterpriseCadasterQueryPlan.Invalid(
                query,
                diagnostics.Count == 0
                    ? "No enabled Legal or Fiscal cadaster Enterprise sources are configured."
                    : string.Join(" ", diagnostics),
                diagnostics);
        }

        return new CompareEnterpriseCadasterQueryPlan(true, query, requests, diagnostics, null);
    }

    public async Task<CompareEnterpriseCadasterEvidenceResult> QueryAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default)
    {
        var settings = getSettings().CompareEnterpriseCadaster;
        var plan = BuildQueryPlan(transaction, geometryPlan, settings);
        if (!plan.IsValid)
        {
            return CompareEnterpriseCadasterEvidenceResult.Failed(
                plan.Query,
                plan.InvalidReason ?? "Enterprise cadaster evidence query plan is invalid.",
                plan.InvalidReason);
        }

        if (queryExecutor is null)
        {
            return CompareEnterpriseCadasterEvidenceResult.Ready(
                plan.Query,
                Array.Empty<CompareEnterpriseCadasterEvidenceRecord>(),
                $"Enterprise cadaster evidence query plan is ready for {plan.LayerRequests.Count} source(s). Configure an ArcGIS spatial query executor to retrieve Legal/Fiscal neighbor rows.",
                string.Join(" ", plan.Diagnostics.Where(message => !string.IsNullOrWhiteSpace(message))));
        }

        var records = new List<CompareEnterpriseCadasterEvidenceRecord>();
        var diagnostics = new List<string>(plan.Diagnostics);
        foreach (var request in plan.LayerRequests)
        {
            try
            {
                var layerRows = await queryExecutor.QueryLayerAsync(request, cancellationToken).ConfigureAwait(false);
                records.AddRange(layerRows);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
            {
                diagnostics.Add($"{request.SourceName} query failed: {exception.Message}");
            }
        }

        var sorted = CompareEnterpriseCadasterEvidenceClassifier.Sort(records)
            .ToArray();
        return sorted.Length == 0
            ? CompareEnterpriseCadasterEvidenceResult.NoRecord(plan.Query, getUtcNow(), string.Join(" ", diagnostics))
            : CompareEnterpriseCadasterEvidenceResult.Ready(
                plan.Query,
                sorted,
                $"Enterprise cadaster evidence returned {sorted.Length} candidate parcel row(s).",
                string.Join(" ", diagnostics));
    }

    private static void AddRequest(
        CompareEnterpriseCadasterSettings settings,
        CompareEnterpriseCadasterSourceSettings source,
        string sourceKind,
        CompareWorkingGeometryLoadPlan geometryPlan,
        List<CompareEnterpriseCadasterLayerRequest> requests,
        List<string> diagnostics)
    {
        if (!source.Enabled)
        {
            diagnostics.Add($"{source.SourceName} source is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(source.LayerUrl))
        {
            diagnostics.Add($"{source.SourceName} layer_url is not configured.");
            return;
        }

        var outFields = source.EvidenceFields();
        if (outFields.Count == 0)
        {
            diagnostics.Add($"{source.SourceName} has no evidence fields configured.");
            return;
        }

        requests.Add(new CompareEnterpriseCadasterLayerRequest(
            sourceKind,
            source.SourceName,
            source.LayerUrl,
            geometryPlan.ScopeField,
            geometryPlan.ScopeValue,
            geometryPlan.DefinitionQuery,
            outFields,
            true,
            true,
            settings.ResultLimit,
            settings.PageSize,
            settings.RelationshipToleranceMeters,
            source));
    }
}

public static class CompareEnterpriseCadasterEvidenceClassifier
{
    public static IReadOnlyList<CompareEnterpriseCadasterEvidenceRecord> Sort(
        IEnumerable<CompareEnterpriseCadasterEvidenceRecord> records)
    {
        return records
            .OrderBy(record => record.IsIncluded ? 0 : 1)
            .ThenBy(record => RelationshipRank(record.SpatialRelationship))
            .ThenBy(record => SourceRank(record.SourceKind))
            .ThenBy(record => record.ParcelId ?? record.Pid ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.SourceLabel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ClassifyFromMetrics(
        bool sameReviewMatch,
        bool contains,
        bool within,
        double overlapArea,
        double sharedBoundaryLength,
        bool intersects,
        double tolerance)
    {
        if (sameReviewMatch)
        {
            return CompareSpatialRelationship.SameReviewMatch;
        }

        if (contains)
        {
            return CompareSpatialRelationship.Contains;
        }

        if (within)
        {
            return CompareSpatialRelationship.Within;
        }

        if (overlapArea > Math.Max(0, tolerance))
        {
            return CompareSpatialRelationship.Overlaps;
        }

        if (sharedBoundaryLength > Math.Max(0, tolerance))
        {
            return CompareSpatialRelationship.Touches;
        }

        return intersects ? CompareSpatialRelationship.IntersectsOnly : CompareSpatialRelationship.Unknown;
    }

    private static int RelationshipRank(string? relationship)
    {
        return relationship switch
        {
            CompareSpatialRelationship.SameReviewMatch => 0,
            CompareSpatialRelationship.Overlaps => 1,
            CompareSpatialRelationship.Contains => 2,
            CompareSpatialRelationship.Within => 3,
            CompareSpatialRelationship.Touches => 4,
            CompareSpatialRelationship.IntersectsOnly => 5,
            _ => 6
        };
    }

    private static int SourceRank(string? sourceKind)
    {
        return sourceKind switch
        {
            CompareEnterpriseCadasterSourceKind.Legal => 0,
            CompareEnterpriseCadasterSourceKind.Fiscal => 1,
            _ => 2
        };
    }
}

public static class CompareEnterpriseCadasterSourceKind
{
    public const string Legal = "legal";
    public const string Fiscal = "fiscal";
}

public static class CompareSpatialRelationship
{
    public const string SameReviewMatch = "same/review match";
    public const string Touches = "touches";
    public const string Overlaps = "overlaps";
    public const string Contains = "contains";
    public const string Within = "within";
    public const string IntersectsOnly = "intersects-only";
    public const string Unknown = "unknown";
}

public sealed record CompareEnterpriseCadasterEvidenceQuery(
    string TransactionNumber,
    string? GeometryScopeField,
    string? GeometryScopeValue);

public sealed record CompareEnterpriseCadasterQueryPlan(
    bool IsValid,
    CompareEnterpriseCadasterEvidenceQuery Query,
    IReadOnlyList<CompareEnterpriseCadasterLayerRequest> LayerRequests,
    IReadOnlyList<string> Diagnostics,
    string? InvalidReason)
{
    public static CompareEnterpriseCadasterQueryPlan Invalid(
        CompareEnterpriseCadasterEvidenceQuery query,
        string reason,
        IReadOnlyList<string>? diagnostics = null)
    {
        return new CompareEnterpriseCadasterQueryPlan(
            false,
            query,
            Array.Empty<CompareEnterpriseCadasterLayerRequest>(),
            diagnostics ?? Array.Empty<string>(),
            reason);
    }
}

public sealed record CompareEnterpriseCadasterLayerRequest(
    string SourceKind,
    string SourceName,
    string LayerUrl,
    string GeometryScopeField,
    string GeometryScopeValue,
    string WorkingReviewDefinitionQuery,
    IReadOnlyList<string> OutFields,
    bool RequiresGeometry,
    bool ReturnGeometry,
    int ResultLimit,
    int PageSize,
    double RelationshipToleranceMeters,
    CompareEnterpriseCadasterSourceSettings FieldMap);

public sealed record CompareEnterpriseCadasterEvidenceRecord(
    string SourceKind,
    string SourceLabel,
    string LayerUrl,
    string? ObjectId,
    string? GlobalId,
    string? Suid,
    string? ParcelId,
    string? Pid,
    string? Volume,
    string? Folio,
    string? LandValuationNumber,
    string? OwnerName,
    string? OccupantName,
    string? TaxpayerName,
    string? Parish,
    string SpatialRelationship,
    bool IsIncluded,
    DateTimeOffset QueriedAt,
    string Status,
    string? Diagnostic)
{
    public string DisplayName
    {
        get
        {
            var display = FirstNonBlank(OwnerName, OccupantName, TaxpayerName);
            return string.IsNullOrWhiteSpace(display) ? "(no party)" : display;
        }
    }

    public string DisplaySummary
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(ParcelId) && string.IsNullOrWhiteSpace(Pid) ? null : $"PID: {FirstNonBlank(Pid, ParcelId)}",
                string.IsNullOrWhiteSpace(Volume) && string.IsNullOrWhiteSpace(Folio) ? null : $"Vol/Folio: {Volume ?? string.Empty}/{Folio ?? string.Empty}",
                string.IsNullOrWhiteSpace(LandValuationNumber) ? null : $"Land Val No.: {LandValuationNumber}",
                string.IsNullOrWhiteSpace(Parish) ? null : $"Parish: {Parish}",
                string.IsNullOrWhiteSpace(SpatialRelationship) ? null : $"Relationship: {SpatialRelationship}",
                IsIncluded ? "Included" : "Excluded"
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join("; ", parts);
        }
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}

public sealed record CompareEnterpriseCadasterEvidenceResult(
    bool Success,
    bool Retryable,
    CompareEnterpriseCadasterEvidenceQuery Query,
    IReadOnlyList<CompareEnterpriseCadasterEvidenceRecord> Records,
    string Status,
    string Message,
    string? Diagnostic)
{
    public static CompareEnterpriseCadasterEvidenceResult Ready(
        CompareEnterpriseCadasterEvidenceQuery query,
        IReadOnlyList<CompareEnterpriseCadasterEvidenceRecord> records,
        string message,
        string? diagnostic = null)
    {
        return new CompareEnterpriseCadasterEvidenceResult(
            true,
            false,
            query,
            records,
            records.Count == 0 ? CompareEvidenceStatus.NoRecordReturned : CompareEvidenceStatus.Ready,
            message,
            LegalCadasterQueryResult.Redact(diagnostic));
    }

    public static CompareEnterpriseCadasterEvidenceResult NoRecord(
        CompareEnterpriseCadasterEvidenceQuery query,
        DateTimeOffset queriedAt,
        string? diagnostic = null)
    {
        return new CompareEnterpriseCadasterEvidenceResult(
            true,
            false,
            query,
            Array.Empty<CompareEnterpriseCadasterEvidenceRecord>(),
            CompareEvidenceStatus.NoRecordReturned,
            "No Legal/Fiscal neighbor evidence returned.",
            LegalCadasterQueryResult.Redact(diagnostic ?? $"No Legal/Fiscal neighbor evidence returned for {query.TransactionNumber} at {queriedAt:O}."));
    }

    public static CompareEnterpriseCadasterEvidenceResult Failed(
        CompareEnterpriseCadasterEvidenceQuery query,
        string message,
        string? diagnostic = null)
    {
        return new CompareEnterpriseCadasterEvidenceResult(
            false,
            true,
            query,
            Array.Empty<CompareEnterpriseCadasterEvidenceRecord>(),
            CompareEvidenceStatus.ServiceUnavailable,
            LegalCadasterQueryResult.Redact(message),
            LegalCadasterQueryResult.Redact(diagnostic ?? message));
    }
}
