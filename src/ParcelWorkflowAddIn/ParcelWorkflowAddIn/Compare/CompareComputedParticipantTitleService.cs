using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public interface ICompareTitleSourceService
{
    Task<CompareTitleSourceSearchResult> SearchCurrentTitlesAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default);

    Task<CompareTitleDownloadResult> DownloadAsync(
        CompareTitleSourceRecord source,
        string volume,
        string folio,
        CaseFolderLayout layout,
        CancellationToken cancellationToken = default);
}

public interface ICompareTitleSourceSelectionService
{
    CompareTitleSourceRecord? Select(IReadOnlyList<CompareTitleSourceRecord> sources);
}

public sealed record CompareTitleSourceSearchResult(
    bool Success,
    IReadOnlyList<CompareTitleSourceRecord> Sources,
    string Message,
    string? Diagnostic)
{
    public static CompareTitleSourceSearchResult NoRecord(string referenceNo, string? diagnostic = null) =>
        new(true, Array.Empty<CompareTitleSourceRecord>(), $"No current title source found for {referenceNo}.", Redact(diagnostic));

    public static CompareTitleSourceSearchResult Failed(string message, string? diagnostic = null) =>
        new(false, Array.Empty<CompareTitleSourceRecord>(), Redact(message), Redact(diagnostic ?? message));

    private static string Redact(string? value)
    {
        return LegalCadasterQueryResult.Redact(value);
    }
}

public sealed record CompareTitleSourceRecord(
    string SourceId,
    string DisplayName,
    string? ReferenceNo,
    string? DocumentType,
    string? Status);

public sealed record CompareTitleDownloadResult(
    bool Success,
    string Message,
    string? FilePath,
    string? Diagnostic)
{
    public static CompareTitleDownloadResult Failed(string message, string? diagnostic = null) =>
        new(false, LegalCadasterQueryResult.Redact(message), null, LegalCadasterQueryResult.Redact(diagnostic ?? message));
}

public sealed class InnolaCompareTitleSourceService : ICompareTitleSourceService
{
    private const string TitleSourceUnauthorizedMessage = "Innola rejected the title source search. Regular ownership search may still work; check source-search permissions or the CT source search contract.";
    private readonly HttpClient httpClient;
    private readonly Func<string, CancellationToken, Task<InnolaSessionEnsureResult>> ensureSession;
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly Func<string, bool> hasInnolaSessionCookie;

    public InnolaCompareTitleSourceService(
        HttpClient httpClient,
        Func<string, CancellationToken, Task<InnolaSessionEnsureResult>> ensureSession,
        Func<DateTimeOffset>? getUtcNow = null,
        Func<string, bool>? hasInnolaSessionCookie = null)
    {
        this.httpClient = httpClient;
        this.ensureSession = ensureSession;
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.hasInnolaSessionCookie = hasInnolaSessionCookie ?? (serverUrl => InnolaHttpClientFactory.HasCookie(serverUrl, "INNOLAID"));
    }

    public async Task<CompareTitleSourceSearchResult> SearchCurrentTitlesAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        var referenceNo = $"{volume.Trim()}/{folio.Trim()}";
        var ensured = await ensureSession("Compare title source search", cancellationToken).ConfigureAwait(false);
        if (!ensured.Success || ensured.Session is null)
        {
            return CompareTitleSourceSearchResult.Failed(
                InnolaApiResilience.LoginRequiredMessage,
                ensured.Message);
        }

        try
        {
            var uri = InnolaHttp.BuildUri(ensured.Session.ServerUrl, $"{InnolaSettings.V4RestPath}search/");
            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["@c"] = "SearchRequest",
                ["searchKind"] = "source",
                ["params"] = new
                {
                    statusLatest = true,
                    status = "reg_status_current",
                    documentTypes = new[] { "st_title" },
                    referenceNo
                },
                ["page"] = 1,
                ["start"] = 0,
                ["limit"] = 25,
                ["orderBy"] = "id",
                ["orderAsc"] = true
            });

            var search = await SendSearchRequestAsync(
                uri,
                ensured.Session.ServerUrl,
                payload,
                ensured.Session.AccessToken,
                cancellationToken).ConfigureAwait(false);
            if (search.Failure is not null)
            {
                return search.Failure;
            }

            var body = search.ResponseBody ?? string.Empty;
            var sources = ResolveSourceRecords(JsonNode.Parse(body)).ToArray();
            return sources.Length == 0
                ? CompareTitleSourceSearchResult.NoRecord(referenceNo, BuildNoRecordDiagnostic(body))
                : new CompareTitleSourceSearchResult(
                    true,
                    sources,
                    $"{sources.Length} current title source record(s) found for {referenceNo}.",
                    null);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or JsonException
            or IOException
            or InvalidOperationException
            or TaskCanceledException
            or UriFormatException)
        {
            return CompareTitleSourceSearchResult.Failed("Title source search failed. Try again.", exception.Message);
        }
    }

    private async Task<(string? ResponseBody, CompareTitleSourceSearchResult? Failure)> SendSearchRequestAsync(
        Uri uri,
        string serverUrl,
        string payload,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var currentUri = uri;
        var currentServerUrl = serverUrl;
        var currentAccessToken = accessToken;
        using var request = CreateSearchRequest(currentUri, currentServerUrl, payload, currentAccessToken, includeAccessToken: true);
        using var response = await InnolaApiResilience.SendWithAuthorizationRefreshAsync(
            httpClient,
            new InnolaApiOperation("Compare title source search"),
            () => CreateSearchRequest(currentUri, currentServerUrl, payload, currentAccessToken, includeAccessToken: true),
            async retryCancellationToken =>
            {
                var refreshed = await ensureSession("Compare title source search auth retry", retryCancellationToken).ConfigureAwait(false);
                if (!HasRequiredSession(refreshed.Session))
                {
                    return false;
                }

                currentServerUrl = refreshed.Session!.ServerUrl;
                currentAccessToken = refreshed.Session.AccessToken;
                currentUri = InnolaHttp.BuildUri(currentServerUrl, $"{InnolaSettings.V4RestPath}search/");
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), null);
        }

        var failureBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (ShouldRetryWithoutAccessToken(response.StatusCode, currentServerUrl, request))
        {
            using var cookieOnlyRequest = CreateSearchRequest(currentUri, currentServerUrl, payload, currentAccessToken, includeAccessToken: false);
            using var cookieOnlyResponse = await InnolaApiResilience.SendAsync(
                httpClient,
                new InnolaApiOperation("Compare title source search cookie-only"),
                () => CreateSearchRequest(currentUri, currentServerUrl, payload, currentAccessToken, includeAccessToken: false),
                cancellationToken).ConfigureAwait(false);
            if (cookieOnlyResponse.IsSuccessStatusCode)
            {
                return (await cookieOnlyResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), null);
            }

            var cookieOnlyFailureBody = await cookieOnlyResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var cookieOnlyFailureMessage = InnolaApiResilience.IsAuthorizationFailure(cookieOnlyResponse.StatusCode)
                ? TitleSourceUnauthorizedMessage
                : "Title source search failed. Try again.";
            return (null, CompareTitleSourceSearchResult.Failed(
                cookieOnlyFailureMessage,
                $"{BuildFailureDiagnostic(cookieOnlyResponse.StatusCode, currentServerUrl, cookieOnlyRequest, "Compare title source search cookie-only retry", cookieOnlyFailureBody)} Initial Access-Token response was {(int)response.StatusCode} {response.StatusCode}: {failureBody}"));
        }

        var failureMessage = InnolaApiResilience.IsAuthorizationFailure(response.StatusCode)
            ? TitleSourceUnauthorizedMessage
            : "Title source search failed. Try again.";
        return (null, CompareTitleSourceSearchResult.Failed(
            failureMessage,
            BuildFailureDiagnostic(response.StatusCode, currentServerUrl, request, "Compare title source search", failureBody)));
    }

    private HttpRequestMessage CreateSearchRequest(
        Uri uri,
        string serverUrl,
        string payload,
        string? accessToken,
        bool includeAccessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        ApplyInnolaWebSearchHeaders(request, serverUrl);
        if (includeAccessToken && !string.IsNullOrWhiteSpace(accessToken) && !accessToken.Equals(InnolaHttp.SessionCookieAccessToken, StringComparison.Ordinal))
        {
            InnolaHttp.ApplyAuthHeaders(request, accessToken);
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

    private static void ApplyInnolaWebSearchHeaders(HttpRequestMessage request, string serverUrl)
    {
        var normalizedServer = InnolaHttp.NormalizeServerUrl(serverUrl);
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        request.Headers.TryAddWithoutValidation("Origin", normalizedServer.TrimEnd('/'));
        request.Headers.Referrer = new Uri(normalizedServer);
    }

    private string BuildFailureDiagnostic(
        HttpStatusCode statusCode,
        string serverUrl,
        HttpRequestMessage request,
        string operation,
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

        var detail = LegalCadasterQueryResult.Redact(responseBody)
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

    private static string BuildNoRecordDiagnostic(string responseBody)
    {
        try
        {
            var root = JsonNode.Parse(responseBody);
            var rows = ResolveArray(root);
            var rootFields = root is JsonObject obj
                ? string.Join(", ", obj.Select(pair => pair.Key))
                : root?.GetType().Name ?? "null";
            return $"Innola title source search returned {rows.Count} raw row(s), but no source id could be mapped. Root fields: {rootFields}.";
        }
        catch (JsonException exception)
        {
            return $"Innola title source search returned malformed JSON: {exception.Message}";
        }
    }

    public async Task<CompareTitleDownloadResult> DownloadAsync(
        CompareTitleSourceRecord source,
        string volume,
        string folio,
        CaseFolderLayout layout,
        CancellationToken cancellationToken = default)
    {
        var ensured = await ensureSession("Compare title source object read", cancellationToken).ConfigureAwait(false);
        if (!ensured.Success || ensured.Session is null)
        {
            return CompareTitleDownloadResult.Failed(InnolaApiResilience.LoginRequiredMessage, ensured.Message);
        }

        try
        {
            var sourceObject = await ReadSourceObjectAsync(ensured.Session, source.SourceId, cancellationToken).ConfigureAwait(false);
            var bodyId = ResolveBodyId(sourceObject);
            if (string.IsNullOrWhiteSpace(bodyId))
            {
                return CompareTitleDownloadResult.Failed("Selected title source has no downloadable body.");
            }

            ensured = await ensureSession("Compare title document download", cancellationToken).ConfigureAwait(false);
            if (!ensured.Success || ensured.Session is null)
            {
                return CompareTitleDownloadResult.Failed(InnolaApiResilience.LoginRequiredMessage, ensured.Message);
            }

            var downloadUri = InnolaHttp.BuildUri(
                ensured.Session.ServerUrl,
                $"{InnolaSettings.V4RestPath}source/download?noredirect=1&bodyId={Uri.EscapeDataString(bodyId)}");
            var currentDownloadUri = downloadUri;
            var currentDownloadServerUrl = ensured.Session.ServerUrl;
            var currentDownloadAccessToken = ensured.Session.AccessToken;
            using var response = await InnolaApiResilience.SendWithAuthorizationRefreshAsync(
                httpClient,
                new InnolaApiOperation("Compare title document download"),
                () => CreateAuthenticatedGetRequest(currentDownloadUri, currentDownloadAccessToken),
                async retryCancellationToken =>
                {
                    var refreshed = await ensureSession("Compare title document download auth retry", retryCancellationToken).ConfigureAwait(false);
                    if (!HasRequiredSession(refreshed.Session))
                    {
                        return false;
                    }

                    currentDownloadServerUrl = refreshed.Session!.ServerUrl;
                    currentDownloadAccessToken = refreshed.Session.AccessToken;
                    currentDownloadUri = InnolaHttp.BuildUri(
                        currentDownloadServerUrl,
                        $"{InnolaSettings.V4RestPath}source/download?noredirect=1&bodyId={Uri.EscapeDataString(bodyId)}");
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return CompareTitleDownloadResult.Failed(
                    "Title document download failed. Try again.",
                    $"Title document download failed: {(int)response.StatusCode} {response.StatusCode}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return CompareTitleDownloadResult.Failed("Title document download returned an empty file.");
            }

            var filePath = WriteDownloadedTitle(layout, volume, folio, bytes);
            return new CompareTitleDownloadResult(
                true,
                $"Downloaded title document {Path.GetFileName(filePath)}.",
                filePath,
                null);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or JsonException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or TaskCanceledException
            or UriFormatException)
        {
            return CompareTitleDownloadResult.Failed("Title document download failed. Try again.", exception.Message);
        }
    }

    private async Task<JsonObject?> ReadSourceObjectAsync(InnolaSession session, string sourceId, CancellationToken cancellationToken)
    {
        var currentUri = InnolaHttp.BuildUri(
            session.ServerUrl,
            $"{InnolaSettings.V4RestPath}portal/ladm-objects/{Uri.EscapeDataString(sourceId)}?typeKeyId=source");
        var currentServerUrl = session.ServerUrl;
        var currentAccessToken = session.AccessToken;
        using var response = await InnolaApiResilience.SendWithAuthorizationRefreshAsync(
            httpClient,
            new InnolaApiOperation("Compare title source object read"),
            () => CreateAuthenticatedGetRequest(currentUri, currentAccessToken),
            async retryCancellationToken =>
            {
                var refreshed = await ensureSession("Compare title source object read auth retry", retryCancellationToken).ConfigureAwait(false);
                if (!HasRequiredSession(refreshed.Session))
                {
                    return false;
                }

                currentServerUrl = refreshed.Session!.ServerUrl;
                currentAccessToken = refreshed.Session.AccessToken;
                currentUri = InnolaHttp.BuildUri(
                    currentServerUrl,
                    $"{InnolaSettings.V4RestPath}portal/ladm-objects/{Uri.EscapeDataString(sourceId)}?typeKeyId=source");
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Title source object read failed: {(int)response.StatusCode} {response.StatusCode}");
        }

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)) as JsonObject;
    }

    private static HttpRequestMessage CreateAuthenticatedGetRequest(Uri uri, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        InnolaHttp.ApplyAuthHeaders(request, accessToken);
        return request;
    }

    private static bool HasRequiredSession(InnolaSession? session)
    {
        return session is not null
            && !string.IsNullOrWhiteSpace(session.ServerUrl)
            && !string.IsNullOrWhiteSpace(session.AccessToken);
    }

    private string WriteDownloadedTitle(CaseFolderLayout layout, string volume, string folio, byte[] bytes)
    {
        Directory.CreateDirectory(layout.SourceDirectory);
        var fileName = $"Repo_{SanitizeFilePart(volume)}_{SanitizeFilePart(folio)}.pdf";
        var destinationPath = GetAvailableDestinationPath(layout.SourceDirectory, fileName);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        if (!IsPathInside(layout.SourceDirectory, fullDestinationPath))
        {
            throw new InvalidOperationException("Downloaded title file must stay inside the Case Folder source area.");
        }

        File.WriteAllBytes(fullDestinationPath, bytes);
        AddManifestSource(layout, fullDestinationPath);
        return fullDestinationPath;
    }

    private void AddManifestSource(CaseFolderLayout layout, string fullDestinationPath)
    {
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        var fileInfo = new FileInfo(fullDestinationPath);
        var entry = new ManifestSourceFile(
            fullDestinationPath,
            fullDestinationPath,
            ".pdf",
            fileInfo.Length,
            getUtcNow().UtcDateTime.ToString("O"),
            "compare_title_source",
            "st_title");
        var updated = manifest with
        {
            Payload = manifest.Payload with
            {
                SourceFiles = manifest.Payload.SourceFiles.Concat(new[] { entry }).ToArray()
            }
        };
        ManifestSerializer.Write(layout.ManifestPath, updated);
    }

    private static IEnumerable<CompareTitleSourceRecord> ResolveSourceRecords(JsonNode? node)
    {
        return ResolveArray(node)
            .OfType<JsonObject>()
            .Select(MapSourceRecord)
            .Where(record => record is not null)
            .Cast<CompareTitleSourceRecord>();
    }

    private static CompareTitleSourceRecord? MapSourceRecord(JsonObject item)
    {
        var source = item["source"] as JsonObject ?? item["object"] as JsonObject ?? item;
        var id = ReadString(source, "id", "uid", "uuid", "sourceId", "source_id")
            ?? ReadString(item, "id", "uid", "uuid", "sourceId", "source_id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var referenceNo = ReadString(source, "referenceNo", "reference_no", "reference", "titleNo", "title_no");
        var documentType = ReadString(source, "documentType", "document_type", "typeKeyId", "type_key_id");
        var status = ReadString(source, "status", "registrationStatus", "registration_status");
        var display = ReadString(source, "displayName", "display_name", "name", "documentName", "document_name", "title")
            ?? referenceNo
            ?? id;
        return new CompareTitleSourceRecord(id.Trim(), display.Trim(), referenceNo, documentType, status);
    }

    private static string? ResolveBodyId(JsonNode? node)
    {
        if (node is not JsonObject root)
        {
            return null;
        }

        return ReadString(root, "bodyId", "body_id")
            ?? ReadString(root["body"] as JsonObject, "id", "uid", "uuid")
            ?? ReadString(root["documentBody"] as JsonObject, "id", "uid", "uuid")
            ?? ReadString(root["source"]?["body"] as JsonObject, "id", "uid", "uuid")
            ?? ReadString(root["data"]?["body"] as JsonObject, "id", "uid", "uuid");
    }

    private static IReadOnlyList<JsonNode> ResolveArray(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            return array.OfType<JsonNode>().ToArray();
        }

        if (node is not JsonObject obj)
        {
            return Array.Empty<JsonNode>();
        }

        foreach (var name in new[] { "data", "records", "items", "result", "results" })
        {
            if (obj[name] is JsonArray nestedArray)
            {
                return nestedArray.OfType<JsonNode>().ToArray();
            }

            if (obj[name] is JsonObject nestedObject)
            {
                var nested = ResolveArray(nestedObject);
                return nested.Count > 0 ? nested : new[] { nestedObject };
            }
        }

        return new[] { obj };
    }

    private static string? ReadString(JsonObject? node, params string[] names)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (node[name] is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (value.TryGetValue<int>(out var intValue))
                {
                    return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        return null;
    }

    private static string SanitizeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(character =>
            invalid.Contains(character) || character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar
                ? '_'
                : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "blank" : sanitized;
    }

    private static string GetAvailableDestinationPath(string sourceDirectory, string fileName)
    {
        var destination = Path.Combine(sourceDirectory, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var index = 2;
        while (true)
        {
            destination = Path.Combine(sourceDirectory, $"{name}_{index}{extension}");
            if (!File.Exists(destination))
            {
                return destination;
            }

            index++;
        }
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MessageBoxCompareTitleSourceSelectionService : ICompareTitleSourceSelectionService
{
    public CompareTitleSourceRecord? Select(IReadOnlyList<CompareTitleSourceRecord> sources)
    {
        return CompareTitleSourceSelectionWindow.ShowDialogFor(sources);
    }
}

public sealed class UnsupportedCompareTitleSourceService : ICompareTitleSourceService
{
    public Task<CompareTitleSourceSearchResult> SearchCurrentTitlesAsync(
        string volume,
        string folio,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CompareTitleSourceSearchResult.Failed("Title source search is not configured for this Compare workspace."));
    }

    public Task<CompareTitleDownloadResult> DownloadAsync(
        CompareTitleSourceRecord source,
        string volume,
        string folio,
        CaseFolderLayout layout,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CompareTitleDownloadResult.Failed("Title document download is not configured for this Compare workspace."));
    }
}

public sealed class AutoCompareTitleSourceSelectionService : ICompareTitleSourceSelectionService
{
    public CompareTitleSourceRecord? Select(IReadOnlyList<CompareTitleSourceRecord> sources)
    {
        return sources.FirstOrDefault();
    }
}
