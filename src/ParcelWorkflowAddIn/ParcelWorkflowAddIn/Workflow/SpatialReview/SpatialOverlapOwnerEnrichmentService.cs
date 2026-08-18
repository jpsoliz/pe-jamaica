using ParcelWorkflowAddIn.Compare;
using System.Globalization;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

public sealed class SpatialOverlapOwnerEnrichmentService
{
    private static readonly string[] IdentifierPriorityOrder =
    {
        "PID",
        "parcel_id",
        "volume_folio",
        "landval_no",
        "r_number",
        "pe_number",
        "pd_number"
    };

    private readonly ILegalCadasterQueryService legalCadasterQueryService;

    public SpatialOverlapOwnerEnrichmentService(ILegalCadasterQueryService legalCadasterQueryService)
    {
        this.legalCadasterQueryService = legalCadasterQueryService ?? throw new ArgumentNullException(nameof(legalCadasterQueryService));
    }

    public async Task<SpatialOverlapReviewDocument> EnrichAsync(
        SpatialOverlapReviewDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Records.Count == 0)
        {
            return document;
        }

        var cache = new Dictionary<string, SpatialOverlapReviewOwnerEnrichment>(StringComparer.OrdinalIgnoreCase);
        var enrichedRecords = new List<SpatialOverlapReviewRecord>(document.Records.Count);
        foreach (var record in document.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = ResolveRequest(record);
            if (request is null)
            {
                var unavailable = BuildIdentifierUnavailableEnrichment(record);
                enrichedRecords.Add(record with
                {
                    EnrichmentStatus = unavailable.Status,
                    OwnerEnrichment = unavailable
                });
                continue;
            }

            if (!cache.TryGetValue(request.CacheKey, out var enrichment))
            {
                enrichment = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                cache[request.CacheKey] = enrichment;
            }

            enrichedRecords.Add(record with
            {
                EnrichmentStatus = enrichment.Status,
                OwnerEnrichment = enrichment
            });
        }

        return document with
        {
            Records = enrichedRecords
        };
    }

    private async Task<SpatialOverlapReviewOwnerEnrichment> ExecuteAsync(
        EnrichmentRequest request,
        CancellationToken cancellationToken)
    {
        LegalCadasterQueryResult result = request.Kind switch
        {
            "PID" => await legalCadasterQueryService.QueryByParcelIdAsync(request.Value, cancellationToken).ConfigureAwait(false),
            "parcel_id" => await legalCadasterQueryService.QueryByParcelIdAsync(request.Value, cancellationToken).ConfigureAwait(false),
            "volume_folio" => await legalCadasterQueryService.QueryByVolumeFolioAsync(
                request.Volume ?? string.Empty,
                request.Folio ?? string.Empty,
                cancellationToken).ConfigureAwait(false),
            "landval_no" => await legalCadasterQueryService.QueryByLandValuationNumberAsync(request.Value, null, cancellationToken).ConfigureAwait(false),
            _ => LegalCadasterQueryResult.Failed(
                new LegalCadasterQuery("unsupported", null, null, null),
                "Owner enrichment does not support the captured identifier kind.",
                $"Identifier kind '{request.Kind}' is not routed to an Innola owner query.")
        };

        if (!result.Success)
        {
            return new SpatialOverlapReviewOwnerEnrichment(
                SpatialOverlapReviewEnrichmentStatuses.QueryFailed,
                request.Kind,
                request.DisplayValue,
                result.Query.QueryKind.Equals("unsupported", StringComparison.OrdinalIgnoreCase)
                    ? request.CacheKey
                    : LegalCadasterQueryResult.BuildLegalQueryKey(result.Query),
                "Innola/LTF owner enrichment could not be completed for this overlap row.",
                SanitizeDiagnostic(result.Diagnostic ?? result.Message),
                Array.Empty<SpatialOverlapReviewOwnerMatch>());
        }

        if (result.Records.Count == 0)
        {
            return new SpatialOverlapReviewOwnerEnrichment(
                SpatialOverlapReviewEnrichmentStatuses.NoOwnerMatchFound,
                request.Kind,
                request.DisplayValue,
                LegalCadasterQueryResult.BuildLegalQueryKey(result.Query),
                "No owner or property record matched the captured identifier.",
                SanitizeDiagnostic(result.Diagnostic),
                Array.Empty<SpatialOverlapReviewOwnerMatch>());
        }

        var matches = result.Records
            .Select(record => new SpatialOverlapReviewOwnerMatch(
                record.OwnerName,
                record.PartyRole,
                record.ParcelId,
                record.Volume,
                record.Folio,
                record.LandValuationNumber,
                record.Parish,
                record.PropertyType,
                record.Tenure,
                record.RegisteredAt?.ToString("O", CultureInfo.InvariantCulture),
                record.Status,
                record.QueryKey,
                SanitizeDiagnostic(record.Diagnostic)))
            .ToArray();

        var status = matches.Length > 1
            ? SpatialOverlapReviewEnrichmentStatuses.MultipleMatches
            : SpatialOverlapReviewEnrichmentStatuses.Matched;
        var message = matches.Length > 1
            ? $"Innola/LTF returned {matches.Length} owner/property rows for this identifier."
            : "Innola/LTF returned one owner/property row for this identifier.";

        return new SpatialOverlapReviewOwnerEnrichment(
            status,
            request.Kind,
            request.DisplayValue,
            LegalCadasterQueryResult.BuildLegalQueryKey(result.Query),
            message,
            SanitizeDiagnostic(result.Diagnostic),
            matches);
    }

    private static EnrichmentRequest? ResolveRequest(SpatialOverlapReviewRecord record)
    {
        foreach (var kind in IdentifierPriorityOrder)
        {
            switch (kind)
            {
                case "PID" when !string.IsNullOrWhiteSpace(record.Pid):
                    return new EnrichmentRequest("PID", record.Pid!.Trim(), $"PID {record.Pid!.Trim()}");
                case "parcel_id" when string.IsNullOrWhiteSpace(record.Pid) && !string.IsNullOrWhiteSpace(record.ParcelId):
                    return new EnrichmentRequest("parcel_id", record.ParcelId!.Trim(), $"Parcel Id {record.ParcelId!.Trim()}");
                case "volume_folio" when !string.IsNullOrWhiteSpace(record.Volume) && !string.IsNullOrWhiteSpace(record.Folio):
                    var volume = record.Volume!.Trim();
                    var folio = record.Folio!.Trim();
                    return new EnrichmentRequest("volume_folio", $"{volume}/{folio}", $"Vol/Folio {volume}/{folio}", volume, folio);
                case "landval_no" when !string.IsNullOrWhiteSpace(record.LandValuationNumber):
                    return new EnrichmentRequest("landval_no", record.LandValuationNumber!.Trim(), $"Land Val No. {record.LandValuationNumber!.Trim()}");
                case "r_number" when !string.IsNullOrWhiteSpace(record.RNumber):
                    return new EnrichmentRequest("r_number", record.RNumber!.Trim(), $"R Number {record.RNumber!.Trim()}");
                case "pe_number" when !string.IsNullOrWhiteSpace(record.PeNumber):
                    return new EnrichmentRequest("pe_number", record.PeNumber!.Trim(), $"PE Number {record.PeNumber!.Trim()}");
                case "pd_number" when !string.IsNullOrWhiteSpace(record.DpNumber):
                    return new EnrichmentRequest("pd_number", record.DpNumber!.Trim(), $"PD Number {record.DpNumber!.Trim()}");
            }
        }

        return null;
    }

    private static SpatialOverlapReviewOwnerEnrichment BuildIdentifierUnavailableEnrichment(SpatialOverlapReviewRecord record)
    {
        var presentButUnsupported = FirstPresentUnsupportedIdentifier(record);
        var message = presentButUnsupported is null
            ? "No supported identifier is available for Innola/LTF owner enrichment."
            : $"Only {presentButUnsupported.DisplayLabel} is present for this overlap row, and the current Innola/LTF owner query path does not use it directly.";

        return new SpatialOverlapReviewOwnerEnrichment(
            SpatialOverlapReviewEnrichmentStatuses.IdentifierUnavailable,
            presentButUnsupported?.Kind,
            presentButUnsupported?.Value,
            null,
            message,
            null,
            Array.Empty<SpatialOverlapReviewOwnerMatch>());
    }

    private static UnsupportedIdentifier? FirstPresentUnsupportedIdentifier(SpatialOverlapReviewRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.RNumber))
        {
            return new UnsupportedIdentifier("r_number", record.RNumber!.Trim(), "R Number");
        }

        if (!string.IsNullOrWhiteSpace(record.PeNumber))
        {
            return new UnsupportedIdentifier("pe_number", record.PeNumber!.Trim(), "PE Number");
        }

        if (!string.IsNullOrWhiteSpace(record.DpNumber))
        {
            return new UnsupportedIdentifier("pd_number", record.DpNumber!.Trim(), "PD Number");
        }

        return null;
    }

    private static string? SanitizeDiagnostic(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : LegalCadasterQueryResult.Redact(value).Trim();
    }

    private sealed record EnrichmentRequest(
        string Kind,
        string Value,
        string DisplayValue,
        string? Volume = null,
        string? Folio = null)
    {
        public string CacheKey => Kind.Equals("volume_folio", StringComparison.OrdinalIgnoreCase)
            ? $"volume_folio={Volume ?? string.Empty}/{Folio ?? string.Empty}"
            : $"{Kind}={Value}";
    }

    private sealed record UnsupportedIdentifier(string Kind, string Value, string DisplayLabel);
}
