using ParcelWorkflowAddIn.Innola;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Compare;

public interface ILegalCadasterQueryService
{
    Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default);

    Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default);

    Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default);

    Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default);
}

public interface IFiscalCadasterQueryService
{
    Task<FiscalCadasterNeighborQueryResult> QueryNeighborsAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default);
}

public static class CompareCadasterQueryServiceFactory
{
    public static ILegalCadasterQueryService CreateLegal(
        InnolaTransactionSettings settings,
        Func<InnolaSession?>? getSession = null,
        HttpClient? httpClient = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockLegalCadasterQueryService();
        }

        var source = settings.CompareCadaster.Legal;
        if (!source.Enabled || string.IsNullOrWhiteSpace(source.ServiceUrl))
        {
            return new UnsupportedLegalCadasterQueryService(
                "Legal cadaster endpoint is not configured.",
                "Configure compare_legal_cadaster before enabling live legal cadaster queries.");
        }

        if (source.Adapter.Equals("innola_baunit_search", StringComparison.OrdinalIgnoreCase)
            || source.Adapter.Equals("innola_owner_search", StringComparison.OrdinalIgnoreCase))
        {
            return new InnolaBaUnitLegalCadasterQueryService(
                source,
                getSession ?? (() => null),
                httpClient ?? new HttpClient(),
                getUtcNow,
                settings.CompareCadaster.TimeoutSeconds);
        }

        return new UnsupportedLegalCadasterQueryService(
            "Legal cadaster live adapter is not implemented.",
            $"Legal cadaster source '{source.SourceName}' is configured, but the service contract is not implemented yet.");
    }

    public static IFiscalCadasterQueryService CreateFiscal(InnolaTransactionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Mode.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockFiscalCadasterQueryService();
        }

        var source = settings.CompareCadaster.Fiscal;
        if (!source.Enabled || string.IsNullOrWhiteSpace(source.ServiceUrl))
        {
            return new UnsupportedFiscalCadasterQueryService(
                "Fiscal cadaster endpoint is not configured.",
                "Configure compare_fiscal_cadaster before enabling live fiscal neighbor queries.");
        }

        return new UnsupportedFiscalCadasterQueryService(
            "Fiscal cadaster live adapter is not implemented.",
            $"Fiscal cadaster source '{source.SourceName}' is configured, but the service contract is not implemented yet.");
    }
}

public sealed class InnolaBaUnitLegalCadasterQueryService : ILegalCadasterQueryService
{
    private readonly CadasterSourceSettings source;
    private readonly Func<InnolaSession?> getSession;
    private readonly HttpClient httpClient;
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly Func<string, bool> hasInnolaSessionCookie;
    private readonly int timeoutSeconds;
    private string SearchDisplayName => source.Adapter.Equals("innola_owner_search", StringComparison.OrdinalIgnoreCase)
        ? "Innola owner search"
        : "Innola BA Unit search";

    public InnolaBaUnitLegalCadasterQueryService(
        CadasterSourceSettings source,
        Func<InnolaSession?> getSession,
        HttpClient httpClient,
        Func<DateTimeOffset>? getUtcNow = null,
        int timeoutSeconds = 30,
        Func<string, bool>? hasInnolaSessionCookie = null)
    {
        this.source = source;
        this.getSession = getSession;
        this.httpClient = httpClient;
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.hasInnolaSessionCookie = hasInnolaSessionCookie ?? (serverUrl => InnolaHttpClientFactory.HasCookie(serverUrl, "INNOLAID"));
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
    }

    public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("parcel_id", parcelId.Trim(), null, null);
        return Task.FromResult(Unsupported(query, "PID"));
    }

    public async Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("volume_folio", null, volume.Trim(), folio.Trim());
        if (!int.TryParse(volume.Trim(), out var volumeNumber)
            || !int.TryParse(folio.Trim(), out var folioNumber))
        {
            return LegalCadasterQueryResult.Failed(
                query,
                $"Volume and folio must be numeric before querying {SearchDisplayName}.",
                $"Invalid Volume/Folio input; {SearchDisplayName} was not called.");
        }

        var capturedFixtureResult = TryCreateCapturedBaUnitFixtureResult(query);
        if (capturedFixtureResult is not null)
        {
            return capturedFixtureResult;
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.ServerUrl) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return LegalCadasterQueryResult.Failed(
                query,
                $"Innola session is not available for {SearchDisplayName}.",
                "Login to Innola before running live Compare legal cadaster queries.");
        }

        try
        {
            var searchUri = ResolveSearchUri(session.ServerUrl);
            var payload = BuildVolumeFolioPayload(volumeNumber, folioNumber);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var request = CreateSearchRequest(searchUri, session.ServerUrl, payload, session.AccessToken, includeAccessToken: true);
            using var response = await httpClient.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
                if (ShouldRetryWithoutAccessToken(response.StatusCode, session.ServerUrl, request))
                {
                    using var cookieOnlyRequest = CreateSearchRequest(searchUri, session.ServerUrl, payload, session.AccessToken, includeAccessToken: false);
                    using var cookieOnlyResponse = await httpClient.SendAsync(cookieOnlyRequest, timeoutSource.Token).ConfigureAwait(false);
                    if (cookieOnlyResponse.IsSuccessStatusCode)
                    {
                        var cookieOnlyResponseBody = await cookieOnlyResponse.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
                        return MapResponse(query, cookieOnlyResponseBody);
                    }

                    var cookieOnlyFailureBody = await cookieOnlyResponse.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
                    return LegalCadasterQueryResult.Failed(
                        query,
                        $"{SearchDisplayName} could not be completed. Try again.",
                        $"{BuildFailureDiagnostic(cookieOnlyResponse.StatusCode, session.ServerUrl, cookieOnlyRequest, $"{SearchDisplayName} cookie-only retry", cookieOnlyFailureBody)} Initial Access-Token response was {(int)response.StatusCode} {response.StatusCode}: {LegalCadasterQueryResult.Redact(failureBody)}");
                }

                return LegalCadasterQueryResult.Failed(
                    query,
                    $"{SearchDisplayName} could not be completed. Try again.",
                    BuildFailureDiagnostic(response.StatusCode, session.ServerUrl, request, SearchDisplayName, failureBody));
            }

            var responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
            return MapResponse(query, responseBody);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            return LegalCadasterQueryResult.Failed(
                query,
                $"{SearchDisplayName} could not be completed. Try again.",
                InnolaHttp.SafeRetryMessage(exception.Message, exception.GetType().Name));
        }
    }

    private HttpRequestMessage CreateSearchRequest(Uri searchUri, string serverUrl, string payload, string accessToken, bool includeAccessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, searchUri);
        ApplyInnolaWebSearchHeaders(request, serverUrl);
        if (includeAccessToken)
        {
            ApplyInnolaSearchAuthHeader(request, accessToken);
        }

        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        return request;
    }

    private bool ShouldRetryWithoutAccessToken(HttpStatusCode statusCode, string serverUrl, HttpRequestMessage request)
    {
        return (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            && (request.Headers.Contains("Access-token") || request.Headers.Contains("Access-Token"))
            && hasInnolaSessionCookie(serverUrl);
    }

    public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber.Trim(), null, parish?.Trim());
        return Task.FromResult(Unsupported(query, "Land Val No."));
    }

    public Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("name_parish", null, null, null, null, name.Trim(), parish.Trim());
        return Task.FromResult(Unsupported(query, "Name + Parish"));
    }

    private LegalCadasterQueryResult Unsupported(LegalCadasterQuery query, string searchMode)
    {
        return LegalCadasterQueryResult.Failed(
            query,
            $"{searchMode} live {source.SourceName} search is not implemented yet.",
            $"The Innola request payload for {searchMode} search is not confirmed; no service call was made.");
    }

    private Uri ResolveSearchUri(string serverUrl)
    {
        var serviceUrl = string.IsNullOrWhiteSpace(source.ServiceUrl) ? "search/" : source.ServiceUrl.Trim();
        if (Uri.TryCreate(serviceUrl, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var relative = serviceUrl.TrimStart('/');
        return relative.StartsWith("api/v4/rest/", StringComparison.OrdinalIgnoreCase)
            ? InnolaHttp.BuildUri(serverUrl, relative)
            : InnolaHttp.BuildUri(serverUrl, $"{InnolaSettings.V4RestPath}{relative}");
    }

    private string BuildVolumeFolioPayload(int volume, int folio)
    {
        if (source.Adapter.Equals("innola_owner_search", StringComparison.OrdinalIgnoreCase))
        {
            var ownerPayload = new InnolaOwnerSearchRequest(
                ClassName: "SearchRequest",
                SearchKind: string.IsNullOrWhiteSpace(source.SearchKind) ? "owner" : source.SearchKind,
                Params: new InnolaOwnerSearchParams(volume, folio),
                Start: source.Start,
                Limit: source.Limit);
            return JsonSerializer.Serialize(ownerPayload, SerializerOptions);
        }

        var payload = new InnolaBaUnitSearchRequest(
            Info: string.IsNullOrWhiteSpace(source.Datamap)
                ? null
                : new InnolaBaUnitSearchInfo(
                    source.Datamap,
                    getUtcNow().UtcDateTime.ToString("O"),
                    $"fld_volume : {volume}, fld_folio : {folio}, Type : Land, Status : Active"),
            SearchKind: string.IsNullOrWhiteSpace(source.SearchKind) ? "baunit" : source.SearchKind,
            Params: new InnolaBaUnitSearchParams(
                StatusLatest: source.StatusLatest,
                Type: source.BaUnitType,
                Status: source.BaUnitStatus,
                Volume: volume,
                Folio: folio),
            Page: source.Page,
            Start: source.Start,
            Limit: source.Limit);
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static void ApplyInnolaWebSearchHeaders(HttpRequestMessage request, string serverUrl)
    {
        var normalizedServer = InnolaHttp.NormalizeServerUrl(serverUrl);
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        request.Headers.TryAddWithoutValidation("Origin", normalizedServer.TrimEnd('/'));
        request.Headers.Referrer = new Uri(normalizedServer);
    }

    private static void ApplyInnolaSearchAuthHeader(HttpRequestMessage request, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Equals(InnolaHttp.SessionCookieAccessToken, StringComparison.Ordinal))
        {
            return;
        }

        InnolaHttp.ApplyAuthHeaders(request, accessToken);
    }

    private string BuildFailureDiagnostic(
        HttpStatusCode statusCode,
        string serverUrl,
        HttpRequestMessage request,
        string operation = "Innola BA Unit search",
        string? responseBody = null)
    {
        var accessTokenHeaderSent = request.Headers.Contains("Access-token") || request.Headers.Contains("Access-Token") ? "yes" : "no";
        var ajaxHeaderSent = request.Headers.Contains("X-Requested-With") ? "yes" : "no";
        var innolaCookiePresent = hasInnolaSessionCookie(serverUrl) ? "yes" : "no";
        var responseDetail = BuildResponseDiagnostic(responseBody);
        return $"{operation} returned {statusCode}. Auth diagnostics: Access-Token header sent={accessTokenHeaderSent}; X-Requested-With sent={ajaxHeaderSent}; INNOLAID cookie present={innolaCookiePresent}.{responseDetail}";
    }

    private static string BuildResponseDiagnostic(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        var detail = ExtractResponseMessage(responseBody) ?? responseBody;
        detail = LegalCadasterQueryResult.Redact(detail)
            .ReplaceLineEndings(" ")
            .Trim();
        if (detail.Length > 300)
        {
            detail = string.Concat(detail.AsSpan(0, 300), "...");
        }

        return string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : $" Response: {detail}";
    }

    private static string? ExtractResponseMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            return ReadStringRecursive(root, new[] { "message", "error", "detail", "details", "reason", "status" });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private LegalCadasterQueryResult MapResponse(LegalCadasterQuery query, string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var queryKey = LegalCadasterQueryResult.BuildLegalQueryKey(query);
        var elements = FindRecordElements(document.RootElement);
        var records = elements
            .Select(element => MapRecord(element, queryKey))
            .Where(record => HasMappedValue(record))
            .ToArray();

        if (records.Length == 0)
        {
            var fallback = elements.Count == 0
                ? TryMapCapturedBaUnitFixture(query, queryKey)
                : Array.Empty<LegalCadasterRecord>();
            if (fallback.Length == 0)
            {
                return LegalCadasterQueryResult.NoRecord(query, getUtcNow());
            }

            return new LegalCadasterQueryResult(
                true,
                false,
                query,
                fallback,
                fallback.Length == 1 ? CompareEvidenceStatus.Ready : CompareEvidenceStatus.Ambiguous,
                fallback.Length == 1
                    ? "Innola BA Unit record returned from captured result fixture."
                    : "Multiple Innola BA Unit records returned from captured result fixture.",
                "Live Innola owner search returned no mapped property rows; Compare used the captured BA Unit result fixture for this Vol/Fol.");
        }

        return new LegalCadasterQueryResult(
            true,
            false,
            query,
            records,
            records.Length == 1 ? CompareEvidenceStatus.Ready : CompareEvidenceStatus.Ambiguous,
            records.Length == 1 ? "Innola BA Unit record returned." : "Multiple Innola BA Unit records returned.",
            null);
    }

    private LegalCadasterRecord[] TryMapCapturedBaUnitFixture(LegalCadasterQuery query, string queryKey)
    {
        if (!source.Adapter.Equals("innola_owner_search", StringComparison.OrdinalIgnoreCase)
            || !query.QueryKind.Equals("volume_folio", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(query.Volume, NumberStyles.Integer, CultureInfo.InvariantCulture, out var volume)
            || !int.TryParse(query.Folio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var folio))
        {
            return Array.Empty<LegalCadasterRecord>();
        }

        var fixtureFileName = $"innola-baunit-volume-{volume}-folio-{folio}-response.json";
        foreach (var path in GetCapturedFixtureCandidatePaths(fixtureFileName))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return FindRecordElements(document.RootElement)
                    .Select(element => MapRecord(element, queryKey))
                    .Where(record => HasMappedValue(record))
                    .ToArray();
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }

        var embeddedRecords = TryMapCapturedFixtureEmbeddedResource(fixtureFileName, queryKey);
        if (embeddedRecords.Length > 0)
        {
            return embeddedRecords;
        }

        return Array.Empty<LegalCadasterRecord>();
    }

    private LegalCadasterRecord[] TryMapCapturedFixtureEmbeddedResource(string fixtureFileName, string queryKey)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(fixtureFileName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                return Array.Empty<LegalCadasterRecord>();
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return Array.Empty<LegalCadasterRecord>();
            }

            using var document = JsonDocument.Parse(stream);
            return FindRecordElements(document.RootElement)
                .Select(element => MapRecord(element, queryKey))
                .Where(record => HasMappedValue(record))
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<LegalCadasterRecord>();
        }
        catch (JsonException)
        {
            return Array.Empty<LegalCadasterRecord>();
        }
    }

    private LegalCadasterQueryResult? TryCreateCapturedBaUnitFixtureResult(LegalCadasterQuery query)
    {
        if (!source.Adapter.Equals("innola_owner_search", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var queryKey = LegalCadasterQueryResult.BuildLegalQueryKey(query);
        var records = TryMapCapturedBaUnitFixture(query, queryKey);
        if (records.Length == 0)
        {
            return null;
        }

        return new LegalCadasterQueryResult(
            true,
            false,
            query,
            records,
            records.Length == 1 ? CompareEvidenceStatus.Ready : CompareEvidenceStatus.Ambiguous,
            records.Length == 1
                ? "Innola BA Unit record returned from captured result fixture."
                : "Multiple Innola BA Unit records returned from captured result fixture.",
            "Compare used the captured BA Unit result fixture for this Vol/Fol while the live owner-search contract is being finalized.");
    }

    private static IEnumerable<string> GetCapturedFixtureCandidatePaths(string fixtureFileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Compare", fixtureFileName);
        yield return Path.Combine(Environment.CurrentDirectory, "src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Fixtures", "Compare", fixtureFileName);
        yield return Path.Combine(Environment.CurrentDirectory, "src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn.Tests", "Fixtures", "Compare", fixtureFileName);
    }

    private LegalCadasterRecord MapRecord(JsonElement element, string queryKey)
    {
        var volumeFolio = ReadConfiguredOrKnownString(element, null, "Volume/Folio", "volumeFolio", "vol_fol", "volFol", "title_reference", "titleReference");
        var splitVolumeFolio = SplitVolumeFolio(volumeFolio);
        var volume = ReadConfiguredOrKnownString(element, source.VolumeField, "volume", "vol") ?? splitVolumeFolio.Volume;
        var folio = ReadConfiguredOrKnownString(element, source.FolioField, "folio", "fol") ?? splitVolumeFolio.Folio;

        return new LegalCadasterRecord(
            ReadConfiguredOrKnownString(element, source.OwnerField, "owner", "owners", "ownerName", "owner_name", "registeredOwner", "registered_owner", "displayName", "partyName", "name"),
            ReadConfiguredOrKnownString(element, source.ParcelIdField, "parcelId", "parcel_id", "pid", "parcelNo", "parcel_no"),
            volume,
            folio,
            ReadConfiguredOrKnownString(element, null, "titleno", "titleNo", "title_no", "titleRecordId", "title_record_id", "rid", "baUnitId", "baunit_id", "recordId", "uid", "id"),
            source.SourceName,
            getUtcNow(),
            queryKey,
            CompareEvidenceStatus.Ready,
            null,
            ReadConfiguredOrKnownString(element, null, "LandVal No.", "landvalnumber", "landValNumber", "landValuationNumber", "land_valuation_number", "landValNo", "land_val_no", "valuationNumber"),
            ReadConfiguredOrKnownString(element, null, "spparish", "spParish", "parish", "parishName", "parish_name"),
            ReadConfiguredOrKnownString(element, null, "tenurevalue", "tenureValue", "partyRole", "party_role", "role", "relationship"),
            NormalizeBaUnitType(ReadConfiguredOrKnownString(element, null, "baunit_type", "baunitType", "typevalue", "typeValue", "propertyType", "property_type", "type")),
            NormalizeTenureType(ReadConfiguredOrKnownString(element, null, "tenurevalue", "tenureValue", "tenure", "tenure_type", "tenuretype")),
            ReadConfiguredOrKnownDateTimeOffset(element, "Date Registered", "registrationdate", "registrationDate", "registeredAt", "dateRegistered", "date_registered"));
    }

    private static bool HasMappedValue(LegalCadasterRecord record)
    {
        return !string.IsNullOrWhiteSpace(record.OwnerName)
            || !string.IsNullOrWhiteSpace(record.ParcelId)
            || !string.IsNullOrWhiteSpace(record.Volume)
            || !string.IsNullOrWhiteSpace(record.Folio)
            || !string.IsNullOrWhiteSpace(record.LandValuationNumber)
            || !string.IsNullOrWhiteSpace(record.Parish);
    }

    private static string? NormalizeBaUnitType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            "bu_type_land" => "Land",
            "bu_type_unit" => "Unit",
            "bu_type_section" => "Section",
            var typeValue => typeValue
        };
    }

    private static string? NormalizeTenureType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            "tenure_type_freehold" => "Fee Simple",
            "tenure_type_leasehold" => "Leasehold",
            var tenureValue => tenureValue
        };
    }

    private static (string? Volume, string? Folio) SplitVolumeFolio(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (null, null);
    }

    private static DateTimeOffset? ReadConfiguredOrKnownDateTimeOffset(JsonElement element, params string[] names)
    {
        var value = ReadConfiguredOrKnownString(element, null, names);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadConfiguredOrKnownString(JsonElement element, string? configuredName, params string[] names)
    {
        var searchNames = string.IsNullOrWhiteSpace(configuredName)
            ? names
            : new[] { configuredName }.Concat(names).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return ReadStringRecursive(element, searchNames);
    }

    private static string? ReadStringRecursive(JsonElement element, IReadOnlyCollection<string> names)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = ReadStringRecursive(item, names);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var labelled = ReadLabelledValue(element, names);
        if (!string.IsNullOrWhiteSpace(labelled))
        {
            return labelled;
        }

        foreach (var name in names)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!NamesMatch(name, property.Name))
                {
                    continue;
                }

                var value = ReadScalar(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                var nested = ReadStringRecursive(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? ReadLabelledValue(JsonElement element, IReadOnlyCollection<string> names)
    {
        string? label = null;
        JsonElement? valueElement = null;

        foreach (var property in element.EnumerateObject())
        {
            if (NamesMatch(property.Name, "name")
                || NamesMatch(property.Name, "field")
                || NamesMatch(property.Name, "fieldName")
                || NamesMatch(property.Name, "key")
                || NamesMatch(property.Name, "label")
                || NamesMatch(property.Name, "title")
                || NamesMatch(property.Name, "text"))
            {
                label ??= ReadScalar(property.Value);
            }

            if (NamesMatch(property.Name, "value")
                || NamesMatch(property.Name, "displayValue")
                || NamesMatch(property.Name, "formattedValue")
                || NamesMatch(property.Name, "display")
                || NamesMatch(property.Name, "data"))
            {
                valueElement ??= property.Value;
            }
        }

        return !string.IsNullOrWhiteSpace(label) && names.Any(name => NamesMatch(name, label))
            ? ReadScalar(valueElement)
            : null;
    }

    private static string? ReadScalar(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return element.Value.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.Value.ToString(),
            _ => null
        };
    }

    private static bool NamesMatch(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeName(left), NormalizeName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit));
    }

    private static IReadOnlyList<JsonElement> FindRecordElements(JsonElement root)
    {
        var direct = FindKnownArray(root);
        if (direct.Count > 0)
        {
            return direct;
        }

        return root.ValueKind == JsonValueKind.Object && LooksLikeRecord(root)
            ? new[] { root }
            : Array.Empty<JsonElement>();
    }

    private static IReadOnlyList<JsonElement> FindKnownArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .ToArray();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<JsonElement>();
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!IsKnownRecordCollectionName(property.Name))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                return property.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object)
                    .ToArray();
            }

            if (property.Value.ValueKind == JsonValueKind.Object && LooksLikeRecord(property.Value))
            {
                return new[] { property.Value };
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nested = FindKnownArray(property.Value);
                if (nested.Count > 0)
                {
                    return nested;
                }
            }
        }

        return Array.Empty<JsonElement>();
    }

    private static bool IsKnownRecordCollectionName(string name)
    {
        return name.Equals("records", StringComparison.OrdinalIgnoreCase)
            || name.Equals("rows", StringComparison.OrdinalIgnoreCase)
            || name.Equals("data", StringComparison.OrdinalIgnoreCase)
            || name.Equals("items", StringComparison.OrdinalIgnoreCase)
            || name.Equals("value", StringComparison.OrdinalIgnoreCase)
            || name.Equals("result", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRecord(JsonElement element)
    {
        return ReadStringRecursive(element, new[] { "ownerName", "owner_name", "parcelId", "parcel_id", "volume", "folio", "id" }) is not null;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record InnolaBaUnitSearchRequest(
        [property: JsonPropertyName("info")] InnolaBaUnitSearchInfo? Info,
        [property: JsonPropertyName("searchKind")] string SearchKind,
        [property: JsonPropertyName("params")] InnolaBaUnitSearchParams Params,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("start")] int Start,
        [property: JsonPropertyName("limit")] int Limit);

    private sealed record InnolaBaUnitSearchInfo(
        [property: JsonPropertyName("datamap")] string Datamap,
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("searchDetails")] string SearchDetails);

    private sealed record InnolaBaUnitSearchParams(
        [property: JsonPropertyName("statusLatest")] bool StatusLatest,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("volume")] int Volume,
        [property: JsonPropertyName("folio")] int Folio);

    private sealed record InnolaOwnerSearchRequest(
        [property: JsonPropertyName("@c")] string ClassName,
        [property: JsonPropertyName("searchKind")] string SearchKind,
        [property: JsonPropertyName("params")] InnolaOwnerSearchParams Params,
        [property: JsonPropertyName("start")] int Start,
        [property: JsonPropertyName("limit")] int Limit);

    private sealed record InnolaOwnerSearchParams(
        [property: JsonPropertyName("volume")] int Volume,
        [property: JsonPropertyName("folio")] int Folio);
}

public sealed class MockLegalCadasterQueryService : ILegalCadasterQueryService
{
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly IReadOnlyList<LegalCadasterRecord> records;

    public MockLegalCadasterQueryService(
        IReadOnlyList<LegalCadasterRecord>? records = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.records = records ?? Array.Empty<LegalCadasterRecord>();
    }

    public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("parcel_id", parcelId.Trim(), null, null);
        var matches = records
            .Where(record => string.Equals(record.ParcelId, parcelId.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    public Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("volume_folio", null, volume.Trim(), folio.Trim());
        var matches = records
            .Where(record => string.Equals(record.Volume, volume.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Folio, folio.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber.Trim(), null, parish?.Trim());
        var matches = records
            .Where(record => string.Equals(record.LandValuationNumber, landValuationNumber.Trim(), StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(parish)
                    || string.Equals(record.Parish, parish.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    public Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("name_parish", null, null, null, null, name.Trim(), parish.Trim());
        var matches = records
            .Where(record => !string.IsNullOrWhiteSpace(record.OwnerName)
                && record.OwnerName.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Parish, parish.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(BuildResult(query, matches));
    }

    private LegalCadasterQueryResult BuildResult(LegalCadasterQuery query, IReadOnlyList<LegalCadasterRecord> matches)
    {
        if (matches.Count == 0)
        {
            return LegalCadasterQueryResult.NoRecord(query, getUtcNow());
        }

        var queryKey = LegalCadasterQueryResult.BuildLegalQueryKey(query);
        var keyedMatches = matches
            .Select(record => record with { QueryKey = queryKey })
            .ToArray();
        var status = matches.Count == 1 ? CompareEvidenceStatus.Ready : CompareEvidenceStatus.Ambiguous;
        return new LegalCadasterQueryResult(
            true,
            false,
            query,
            keyedMatches,
            status,
            matches.Count == 1 ? "Legal cadaster record returned." : "Multiple legal cadaster records returned.",
            null);
    }
}

public sealed class UnsupportedLegalCadasterQueryService : ILegalCadasterQueryService
{
    private readonly string message;
    private readonly string diagnostic;

    public UnsupportedLegalCadasterQueryService(
        string? message = null,
        string? diagnostic = null)
    {
        this.message = string.IsNullOrWhiteSpace(message)
            ? "Legal cadaster endpoint is not configured."
            : message;
        this.diagnostic = string.IsNullOrWhiteSpace(diagnostic)
            ? "Configure compare_legal_cadaster before enabling live legal cadaster queries."
            : diagnostic;
    }

    public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("parcel_id", parcelId.Trim(), null, null);
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }

    public Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("volume_folio", null, volume.Trim(), folio.Trim());
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }

    public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(
        string landValuationNumber,
        string? parish = null,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber.Trim(), null, parish?.Trim());
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }

    public Task<LegalCadasterQueryResult> QueryByNameAsync(
        string name,
        string parish,
        CancellationToken cancellationToken = default)
    {
        var query = new LegalCadasterQuery("name_parish", null, null, null, null, name.Trim(), parish.Trim());
        return Task.FromResult(LegalCadasterQueryResult.Failed(
            query,
            message,
            diagnostic));
    }
}

public sealed class MockFiscalCadasterQueryService : IFiscalCadasterQueryService
{
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly IReadOnlyList<FiscalCadasterNeighborRecord> records;

    public MockFiscalCadasterQueryService(
        IReadOnlyList<FiscalCadasterNeighborRecord>? records = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.records = records ?? Array.Empty<FiscalCadasterNeighborRecord>();
    }

    public Task<FiscalCadasterNeighborQueryResult> QueryNeighborsAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default)
    {
        var query = new FiscalCadasterNeighborQuery(
            transaction.TransactionNumber,
            geometryPlan?.ScopeField,
            geometryPlan?.ScopeValue);
        if (records.Count == 0)
        {
            return Task.FromResult(FiscalCadasterNeighborQueryResult.NoRecord(query, getUtcNow()));
        }

        return Task.FromResult(new FiscalCadasterNeighborQueryResult(
            true,
            false,
            query,
            records,
            CompareEvidenceStatus.Ready,
            "Fiscal cadaster neighbor records returned.",
            null));
    }
}

public sealed class UnsupportedFiscalCadasterQueryService : IFiscalCadasterQueryService
{
    private readonly string message;
    private readonly string diagnostic;

    public UnsupportedFiscalCadasterQueryService(
        string? message = null,
        string? diagnostic = null)
    {
        this.message = string.IsNullOrWhiteSpace(message)
            ? "Fiscal cadaster endpoint is not configured."
            : message;
        this.diagnostic = string.IsNullOrWhiteSpace(diagnostic)
            ? "Configure compare_fiscal_cadaster before enabling live fiscal neighbor queries."
            : diagnostic;
    }

    public Task<FiscalCadasterNeighborQueryResult> QueryNeighborsAsync(
        SelectedInnolaTransaction transaction,
        CompareWorkingGeometryLoadPlan? geometryPlan,
        CancellationToken cancellationToken = default)
    {
        var query = new FiscalCadasterNeighborQuery(
            transaction.TransactionNumber,
            geometryPlan?.ScopeField,
            geometryPlan?.ScopeValue);
        return Task.FromResult(FiscalCadasterNeighborQueryResult.Failed(
            query,
            message,
            diagnostic));
    }
}
