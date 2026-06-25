using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionDetailService : IInnolaTransactionDetailService
{
    private static readonly IReadOnlyList<ComputeAttachmentSourceTypeDefinition> ConfiguredSourceTypes = InnolaTransactionSettings.Load().ComputeAttachmentSourceTypes;
    private readonly HttpClient httpClient;

    public InnolaTransactionDetailService()
        : this(new HttpClient())
    {
    }

    public InnolaTransactionDetailService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.ServerUrl) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return InnolaTransactionDetailResult.Failure("Could not load transaction. Try again.", "unauthorized");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                InnolaHttp.BuildUri(session.ServerUrl, $"{InnolaSettings.V4RestPath}workflow/tasks/{Uri.EscapeDataString(selectedTransaction.TaskId)}"));
            InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Innola transaction detail failed. TaskId={selectedTransaction.TaskId}; Status={response.StatusCode}.");
                return InnolaTransactionDetailResult.Failure("Could not load transaction. Try again.", response.StatusCode.ToString());
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseBody);
            var taskRoot = ResolveTaskRoot(document.RootElement);
            if (!taskRoot.HasValue)
            {
                Debug.WriteLine($"Innola transaction detail did not include a task object. TaskId={selectedTransaction.TaskId}.");
                return InnolaTransactionDetailResult.Failure("Transaction details were not found.", "not_found");
            }

            var detail = MapDetail(taskRoot.Value, selectedTransaction);
            Debug.WriteLine(
                $"Innola transaction detail mapped. TaskId={detail.TaskId}; TransactionNumber={detail.TransactionNumber}; TransactionId={detail.TransactionId}; ApplicationId={selectedTransaction.ApplicationId ?? "(none)"}; InlineAttachments={detail.Attachments.Count}.");
            if (detail.Attachments.Count == 0)
            {
                var sourceAttachments = await GetScanningApplicationSourceAttachmentsAsync(session, selectedTransaction, detail, cancellationToken).ConfigureAwait(false);
                if (sourceAttachments.Count == 0)
                {
                    sourceAttachments = await GetTransactionSourceAttachmentsAsync(session, selectedTransaction, detail, cancellationToken).ConfigureAwait(false);
                }

                detail = detail with { Attachments = sourceAttachments };
            }

            if (detail.Attachments.Count == 0)
            {
                Debug.WriteLine(
                    $"Innola transaction detail has no attachment metadata after source fallback. TaskId={detail.TaskId}; TransactionNumber={detail.TransactionNumber}; LookupIds={string.Join(",", SourceLookupIds(selectedTransaction, detail))}.");
                return InnolaTransactionDetailResult.Failure("Attachment metadata unavailable for this transaction.", "attachment_metadata_unavailable");
            }

            return InnolaTransactionDetailResult.Succeeded(detail);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            return InnolaTransactionDetailResult.Failure("Could not load transaction. Try again.", exception.GetType().Name);
        }
    }

    public async Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
        InnolaSession session,
        InnolaTransactionDetail detail,
        InnolaAttachmentMetadata attachment,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseServiceReference(attachment.ServiceReference, out var queryName, out var queryValue))
        {
            return InnolaAttachmentContentResult.Failure("Attachment metadata unavailable for this transaction.", "attachment_metadata_unavailable");
        }

        try
        {
            var path = queryName.Equals("bodyId", StringComparison.OrdinalIgnoreCase)
                ? $"{InnolaSettings.V4RestPath}source/download?bodyId={Uri.EscapeDataString(queryValue)}&attachment=false&documentName={Uri.EscapeDataString(attachment.FileName)}"
                : queryName.Equals("scanSourceId", StringComparison.OrdinalIgnoreCase)
                    ? $"{InnolaSettings.RestPath}scanning/source/{Uri.EscapeDataString(queryValue)}/body"
                : $"{InnolaSettings.V4RestPath}source/download?{queryName}={Uri.EscapeDataString(queryValue)}&attachment=false&documentName={Uri.EscapeDataString(attachment.FileName)}";
            var uri = InnolaHttp.BuildUri(session.ServerUrl, path);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);
            Debug.WriteLine($"Innola attachment download starting. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; Reference={attachment.ServiceReference}; Path={path}.");

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Innola attachment download failed. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; Status={response.StatusCode}.");
                return InnolaAttachmentContentResult.Failure("Could not load attachment. Try again.", response.StatusCode.ToString());
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"Innola attachment download completed. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; Bytes={content.Length}.");
            return content.Length == 0
                ? InnolaAttachmentContentResult.Failure("Attachment content was not found.", "not_found")
                : InnolaAttachmentContentResult.Succeeded(content);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            Debug.WriteLine($"Innola attachment download failed. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; Error={exception.GetType().Name}.");
            return InnolaAttachmentContentResult.Failure("Could not load attachment. Try again.", exception.GetType().Name);
        }
    }

    public async Task<InnolaAttachmentUploadResult> UploadAttachmentAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        string fileName,
        string contentType,
        byte[] content,
        string sourceType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.ServerUrl) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return InnolaAttachmentUploadResult.Failure("Could not upload attachment. Try again.", "unauthorized");
        }

        try
        {
            var route = NormalizeUploadRoute(ShellState.AttachmentUploadRoute);
            var bindingMode = NormalizeBindingMode(ShellState.AttachmentUploadBindingMode);
            var uploadMode = NormalizeUploadMode(ShellState.AttachmentUploadMode);
            var query = BuildUploadQuery(selectedTransaction, sourceType, bindingMode, route, uploadMode);
            var requestPath = BuildUploadRequestPath(route, query);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                InnolaHttp.BuildUri(
                    session.ServerUrl,
                    requestPath));
            InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

            using var formData = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            formData.Add(fileContent, "file", fileName);

            if (bindingMode is AttachmentUploadBindingMode.FormOnly or AttachmentUploadBindingMode.QueryAndForm)
            {
                formData.Add(new StringContent(sourceType), "sourceType");
                formData.Add(new StringContent(ResolveUploadTaskValue(selectedTransaction, route, uploadMode)), "taskId");
                if (!string.IsNullOrWhiteSpace(selectedTransaction.TransactionId))
                {
                    formData.Add(new StringContent(selectedTransaction.TransactionId), "transactionId");
                }
            }
            request.Content = formData;
            Debug.WriteLine(
                $"Innola attachment upload starting. TaskId={selectedTransaction.TaskId}; File={fileName}; Bytes={content.Length}; Route={route}; BindingMode={bindingMode}; UploadMode={uploadMode}; SourceType={sourceType}; Path={requestPath}.");

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
                Debug.WriteLine($"Innola attachment upload failed. TaskId={selectedTransaction.TaskId}; File={fileName}; Status={response.StatusCode}; Body={responseBody}.");
                return InnolaAttachmentUploadResult.Failure(
                    $"Could not upload saved resume package ({response.StatusCode}). Try again.",
                    response.StatusCode.ToString());
            }

            var uploadResponseBody = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            if (uploadMode == AttachmentUploadMode.AttachThenRegisterSource)
            {
                var registeredType = ResolveRegisteredType(sourceType);
                var registerResult = await RegisterUploadedSourceAsync(
                    session,
                    selectedTransaction,
                    sourceType,
                    registeredType,
                    uploadResponseBody,
                    cancellationToken).ConfigureAwait(false);
                if (!registerResult.Success)
                {
                    return registerResult;
                }
            }

            Debug.WriteLine($"Innola attachment upload completed. TaskId={selectedTransaction.TaskId}; File={fileName}; Bytes={content.Length}.");
            return InnolaAttachmentUploadResult.Succeeded();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            Debug.WriteLine($"Innola attachment upload failed. TaskId={selectedTransaction.TaskId}; File={fileName}; Error={exception.GetType().Name}.");
            return InnolaAttachmentUploadResult.Failure("Could not upload saved resume package. Try again.", exception.GetType().Name);
        }
    }

    private async Task<InnolaAttachmentUploadResult> RegisterUploadedSourceAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        string sourceType,
        string? registeredType,
        string uploadResponseBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedTransaction.TransactionId))
        {
            return InnolaAttachmentUploadResult.Failure("Could not register uploaded source because the transaction id is unavailable.", "transaction_id_missing");
        }

        JsonNode uploadedSource;
        try
        {
            uploadedSource = JsonNode.Parse(uploadResponseBody)
                ?? throw new JsonException("Upload response was empty.");
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            Debug.WriteLine($"Innola uploaded source registration parse failed. TransactionId={selectedTransaction.TransactionId}; Error={exception.GetType().Name}; Body={SanitizeDiagnostic(uploadResponseBody)}.");
            return InnolaAttachmentUploadResult.Failure("Could not register uploaded source. Try again.", "upload_response_invalid");
        }

        List<JsonNode> existingSources;
        try
        {
            existingSources = await GetAdministrativeSourcesAsync(session, selectedTransaction.TransactionId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException or JsonException)
        {
            Debug.WriteLine($"Innola source registration lookup failed. TransactionId={selectedTransaction.TransactionId}; Error={exception.GetType().Name}.");
            return InnolaAttachmentUploadResult.Failure("Could not load current transaction sources. Try again.", exception.GetType().Name);
        }

        try
        {
            PrepareUploadedSourceForRegistration(
                uploadedSource,
                existingSources,
                sourceType,
                registeredType,
                ShellState.AttachmentRegisteredSpatialUnitId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            Debug.WriteLine($"Innola uploaded source preparation failed. TransactionId={selectedTransaction.TransactionId}; Error={exception.GetType().Name}.");
            return InnolaAttachmentUploadResult.Failure("Could not prepare uploaded source for transaction registration.", exception.GetType().Name);
        }

        existingSources.Add(uploadedSource);
        var payload = new JsonArray(existingSources.Select(node => node.DeepClone()).ToArray());
        using var registerRequest = new HttpRequestMessage(
            HttpMethod.Post,
            InnolaHttp.BuildUri(
                session.ServerUrl,
                $"{InnolaSettings.V4RestPath}administrative/ladm-objects?typeKeyId=source&transactionId={Uri.EscapeDataString(selectedTransaction.TransactionId)}"));
        InnolaHttp.ApplyAuthHeaders(registerRequest, session.AccessToken);
        registerRequest.Content = new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json");

        using var registerResponse = await httpClient.SendAsync(registerRequest, cancellationToken).ConfigureAwait(false);
        if (!registerResponse.IsSuccessStatusCode)
        {
            var responseBody = await ReadResponseBodyAsync(registerResponse, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"Innola uploaded source registration failed. TransactionId={selectedTransaction.TransactionId}; Status={registerResponse.StatusCode}; Body={responseBody}.");
            return InnolaAttachmentUploadResult.Failure(
                $"Could not register uploaded source ({registerResponse.StatusCode}). Try again.",
                registerResponse.StatusCode.ToString());
        }

        Debug.WriteLine($"Innola uploaded source registration completed. TransactionId={selectedTransaction.TransactionId}; SourceCount={existingSources.Count}.");
        return InnolaAttachmentUploadResult.Succeeded();
    }

    private async Task<IReadOnlyList<InnolaAttachmentMetadata>> GetScanningApplicationSourceAttachmentsAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        InnolaTransactionDetail detail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedTransaction.ApplicationId))
        {
            Debug.WriteLine($"Innola scanning application source lookup skipped. TaskId={detail.TaskId}; ApplicationId=(none).");
            return Array.Empty<InnolaAttachmentMetadata>();
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                InnolaHttp.BuildUri(
                    session.ServerUrl,
                    $"{InnolaSettings.RestPath}scanning/application/{Uri.EscapeDataString(selectedTransaction.ApplicationId)}"));
            InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Innola scanning application source lookup failed. ApplicationId={selectedTransaction.ApplicationId}; Status={response.StatusCode}.");
                return Array.Empty<InnolaAttachmentMetadata>();
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseBody);
            var attachments = ExtractAttachments(document.RootElement);
            Debug.WriteLine($"Innola scanning application source lookup completed. ApplicationId={selectedTransaction.ApplicationId}; AttachmentCount={attachments.Count}.");
            return attachments;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            Debug.WriteLine($"Innola scanning application source lookup failed. ApplicationId={selectedTransaction.ApplicationId}; Error={exception.GetType().Name}.");
            return Array.Empty<InnolaAttachmentMetadata>();
        }
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return $"(response body unavailable: {exception.GetType().Name})";
        }
    }

    private static string SanitizeDiagnostic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        var sanitized = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (sanitized.Length > 400)
        {
            sanitized = sanitized[..400] + "...";
        }

        return sanitized;
    }

    private static string NormalizeUploadRoute(string? route)
    {
        var normalized = string.IsNullOrWhiteSpace(route)
            ? "scanning/source/attach"
            : route.Trim().TrimStart('/');
        return normalized;
    }

    private static AttachmentUploadMode NormalizeUploadMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "attach_then_register_source" => AttachmentUploadMode.AttachThenRegisterSource,
            _ => AttachmentUploadMode.AttachOnly
        };
    }

    private static AttachmentUploadBindingMode NormalizeBindingMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "query_only" => AttachmentUploadBindingMode.QueryOnly,
            "form_only" => AttachmentUploadBindingMode.FormOnly,
            _ => AttachmentUploadBindingMode.QueryAndForm
        };
    }

    private static string BuildUploadQuery(SelectedInnolaTransaction selectedTransaction, string sourceType, AttachmentUploadBindingMode bindingMode, string route, AttachmentUploadMode uploadMode)
    {
        if (bindingMode is AttachmentUploadBindingMode.FormOnly)
        {
            return string.Empty;
        }

        return $"?sourceType={Uri.EscapeDataString(sourceType)}&taskId={Uri.EscapeDataString(ResolveUploadTaskValue(selectedTransaction, route, uploadMode))}";
    }

    private static string BuildUploadRequestPath(string route, string query)
    {
        if (route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/{route}{query}";
        }

        if (route.StartsWith("v4/rest/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/api/{route}{query}";
        }

        if (route.StartsWith("rest/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/api/{route}{query}";
        }

        var prefix = route.StartsWith("source/", StringComparison.OrdinalIgnoreCase)
            ? InnolaSettings.V4RestPath
            : InnolaSettings.RestPath;
        return $"{prefix}{route}{query}";
    }

    private static string ResolveUploadTaskValue(SelectedInnolaTransaction selectedTransaction, string route, AttachmentUploadMode uploadMode)
    {
        if (uploadMode == AttachmentUploadMode.AttachThenRegisterSource
            && route.StartsWith("source/sources/attach", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(selectedTransaction.TransactionId))
        {
            return selectedTransaction.TransactionId;
        }

        return selectedTransaction.TaskId;
    }

    private enum AttachmentUploadBindingMode
    {
        QueryOnly,
        FormOnly,
        QueryAndForm
    }

    private enum AttachmentUploadMode
    {
        AttachOnly,
        AttachThenRegisterSource
    }

    private async Task<List<JsonNode>> GetAdministrativeSourcesAsync(
        InnolaSession session,
        string transactionId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            InnolaHttp.BuildUri(
                session.ServerUrl,
                $"{InnolaSettings.V4RestPath}administrative/ladm-objects?typeKeyId=source&transactionId={Uri.EscapeDataString(transactionId)}"));
        InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Administrative source lookup failed: {response.StatusCode}; {responseBody}");
        }

        var responseBodyText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = JsonNode.Parse(responseBodyText);
        return parsed switch
        {
            JsonArray array => array.Select(node => node?.DeepClone()).Where(node => node is not null).Cast<JsonNode>().ToList(),
            _ => throw new JsonException("Administrative source lookup did not return a JSON array.")
        };
    }

    private static void PrepareUploadedSourceForRegistration(
        JsonNode uploadedSource,
        IReadOnlyList<JsonNode> existingSources,
        string sourceType,
        string? registeredType,
        string? spatialUnitId)
    {
        if (uploadedSource is not JsonObject sourceObject)
        {
            throw new InvalidOperationException("Upload response is not a source object.");
        }

        var nextAtId = GetNextAtId(existingSources);
        ForceAtId(sourceObject, ref nextAtId);

        if (sourceObject["body"] is JsonObject bodyObject)
        {
            ForceAtId(bodyObject, ref nextAtId);
        }

        if (sourceObject["link"] is JsonObject linkObject)
        {
            ForceAtId(linkObject, ref nextAtId);
        }

        if (!string.IsNullOrWhiteSpace(registeredType))
        {
            sourceObject["type"] = registeredType;
        }

        if (!string.IsNullOrWhiteSpace(spatialUnitId))
        {
            sourceObject["spatialUnitId"] = spatialUnitId;
        }
    }

    private static string? ResolveRegisteredType(string sourceType)
    {
        if (string.Equals(sourceType, ShellState.ResumeAttachmentSourceType, StringComparison.OrdinalIgnoreCase))
        {
            return ShellState.ResumeAttachmentRegisteredType;
        }

        if (string.Equals(sourceType, ShellState.CompletedAttachmentSourceType, StringComparison.OrdinalIgnoreCase))
        {
            return ShellState.CompletedAttachmentRegisteredType;
        }

        return ShellState.ResumeAttachmentRegisteredType;
    }

    private static int GetNextAtId(IReadOnlyList<JsonNode> sources)
    {
        var maxAtId = 0;
        foreach (var source in sources)
        {
            maxAtId = Math.Max(maxAtId, ReadAtId(source));
            maxAtId = Math.Max(maxAtId, ReadAtId(source?["body"]));
            maxAtId = Math.Max(maxAtId, ReadAtId(source?["link"]));
        }

        return maxAtId + 1;
    }

    private static int ReadAtId(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return 0;
        }

        var raw = obj["atId"]?.GetValue<string>()
            ?? obj["AtId"]?.GetValue<string>()
            ?? obj["@id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var separator = raw.IndexOf(':', StringComparison.Ordinal);
        var numericPart = separator >= 0 ? raw[(separator + 1)..] : raw;
        return int.TryParse(numericPart, out var value) ? value : 0;
    }

    private static void ForceAtId(JsonObject target, ref int nextAtId)
    {
        var value = $"obj:{nextAtId++}";

        if (target["@id"] is not null)
        {
            target["@id"] = value;
            return;
        }

        if (target["atId"] is not null)
        {
            target["atId"] = value;
            return;
        }

        if (target["AtId"] is not null)
        {
            target["AtId"] = value;
            return;
        }

        target["@id"] = value;
    }

    private async Task<IReadOnlyList<InnolaAttachmentMetadata>> GetTransactionSourceAttachmentsAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        InnolaTransactionDetail detail,
        CancellationToken cancellationToken)
    {
        var lookupIds = SourceLookupIds(selectedTransaction, detail).ToArray();
        Debug.WriteLine($"Innola source metadata fallback starting. TaskId={detail.TaskId}; LookupIds={string.Join(",", lookupIds)}.");
        foreach (var transactionId in lookupIds)
        {
            var attachments = await GetTransactionSourceAttachmentsAsync(session, transactionId, cancellationToken).ConfigureAwait(false);
            if (attachments.Count > 0)
            {
                return attachments;
            }
        }

        return Array.Empty<InnolaAttachmentMetadata>();
    }

    private async Task<IReadOnlyList<InnolaAttachmentMetadata>> GetTransactionSourceAttachmentsAsync(
        InnolaSession session,
        string transactionId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                InnolaHttp.BuildUri(
                    session.ServerUrl,
                    $"{InnolaSettings.RestPath}administrative/ladmobjects/getbytransaction?typeKeyId=source&transactionId={Uri.EscapeDataString(transactionId)}"));
            InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Innola source metadata lookup failed. LookupId={transactionId}; Status={response.StatusCode}.");
                return Array.Empty<InnolaAttachmentMetadata>();
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseBody);
            var attachments = ExtractAttachments(document.RootElement);
            Debug.WriteLine($"Innola source metadata lookup completed. LookupId={transactionId}; AttachmentCount={attachments.Count}.");
            return attachments;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            Debug.WriteLine($"Innola source metadata lookup failed. LookupId={transactionId}; Error={exception.GetType().Name}.");
            return Array.Empty<InnolaAttachmentMetadata>();
        }
    }

    private static IEnumerable<string> SourceLookupIds(SelectedInnolaTransaction selectedTransaction, InnolaTransactionDetail detail)
    {
        var ids = new[] { detail.TransactionId, selectedTransaction.TransactionId, selectedTransaction.ApplicationId };
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonElement? ResolveTaskRoot(JsonElement root)
    {
        if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("task", out var task) && task.ValueKind == JsonValueKind.Object)
        {
            return task;
        }

        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return ResolveTaskRoot(value);
        }

        return root;
    }

    private static InnolaTransactionDetail MapDetail(JsonElement task, SelectedInnolaTransaction selected)
    {
        var transaction = TryObject(task, "transaction");
        var application = TryObject(task, "application");
        var transactionId = InnolaHttp.ReadString(task, "transactionId", "transaction_id")
            ?? ReadNested(transaction, "id")
            ?? selected.TransactionId;
        var transactionNumber = ReadNested(transaction, "transactionNo", "transaction_no", "transactionNumber")
            ?? InnolaHttp.ReadString(task, "transactionNo", "transaction_no")
            ?? selected.TransactionNumber;
        var taskName = InnolaHttp.ReadString(task, "name", "taskName", "task_name") ?? selected.TaskName;
        var caseType = InnolaHttp.ReadString(task, "transactionCode", "processKey")
            ?? ReadNested(transaction, "transactionType", "type")
            ?? ReadNested(application, "type");

        return new InnolaTransactionDetail(
            transactionId,
            transactionNumber,
            InnolaHttp.ReadString(task, "id", "taskId", "task_id") ?? selected.TaskId,
            taskName,
            selected.ProcessStep,
            caseType,
            caseType,
            InnolaHttp.ReadString(task, "assignee", "assignedUser", "assigned_user"),
            InnolaHttp.ReadString(task, "role", "group", "assignedGroup", "assigned_group"),
            InnolaHttp.ReadString(task, "assignee", "ownerUser", "owner_user"),
            InnolaHttp.ReadString(task, "status", "taskStatus", "task_status"),
            ExtractAttachments(task));
    }

    private static IReadOnlyList<InnolaAttachmentMetadata> ExtractAttachments(JsonElement task)
    {
        var attachments = new List<InnolaAttachmentMetadata>();
        CollectAttachmentCandidates(task, null, attachments);
        return attachments
            .GroupBy(item => item.AttachmentId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void CollectAttachmentCandidates(JsonElement element, string? parentName, List<InnolaAttachmentMetadata> attachments)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if ((IsAttachmentContainer(parentName) || LooksLikeAttachment(element)) && TryMapAttachment(element) is { } attachment)
            {
                attachments.Add(attachment);
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectAttachmentCandidates(property.Value, property.Name, attachments);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectAttachmentCandidates(item, parentName, attachments);
            }
        }
    }

    private static bool IsAttachmentContainer(string? name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && (name.Contains("source", StringComparison.OrdinalIgnoreCase)
                || name.Contains("attachment", StringComparison.OrdinalIgnoreCase)
                || name.Contains("document", StringComparison.OrdinalIgnoreCase)
                || name.Contains("file", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeAttachment(JsonElement element)
    {
        return element.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Object
            || element.TryGetProperty("bodyId", out _)
            || element.TryGetProperty("fileBodyId", out _)
            || element.TryGetProperty("fileName", out _)
            || element.TryGetProperty("documentName", out _);
    }

    private static InnolaAttachmentMetadata? TryMapAttachment(JsonElement element)
    {
        var body = TryObject(element, "body");
        var fileName = InnolaHttp.ReadString(element, "fileName", "filename", "name", "documentName", "originalName")
            ?? ReadNested(body, "name", "Name", "fileName", "filename");
        var bodyExtension = ReadNested(body, "extension", "Extension");
        if (!string.IsNullOrWhiteSpace(fileName)
            && string.IsNullOrWhiteSpace(Path.GetExtension(fileName))
            && !string.IsNullOrWhiteSpace(bodyExtension))
        {
            fileName = $"{fileName}.{bodyExtension.TrimStart('.')}";
        }

        var bodyId = InnolaHttp.ReadString(element, "bodyId", "fileBodyId")
            ?? ReadNested(body, "id", "Id", "@id");
        var sourceUid = InnolaHttp.ReadString(element, "sourceUid", "uid");
        var sourceId = InnolaHttp.ReadString(element, "sourceId", "id");
        var serviceReference = !string.IsNullOrWhiteSpace(bodyId)
            ? $"body-id:{bodyId}"
            : !string.IsNullOrWhiteSpace(sourceUid)
                ? $"source-uid:{sourceUid}"
                : !string.IsNullOrWhiteSpace(sourceId)
                    ? $"scan-source-id:{sourceId}"
                    : null;
        if (string.IsNullOrWhiteSpace(serviceReference))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName = BuildFallbackSourceFileName(element, body, sourceId, sourceUid);
        }

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            return null;
        }

        var category = InnolaHttp.ReadString(element, "category", "type", "sourceType", "documentType", "tags", "sourceNo", "description");
        var mimeType = InnolaHttp.ReadString(element, "mimeType", "contentType")
            ?? ReadNested(body, "type", "mimeType", "contentType");
        var size = ReadLong(element, "size", "fileSize", "length")
            ?? ReadNestedLong(body, "size", "fileSize", "length");
        var classification = ResolveAttachmentClassification(fileName, category);
        return new InnolaAttachmentMetadata(
            serviceReference,
            fileName,
            Path.GetExtension(fileName).ToLowerInvariant(),
            mimeType,
            classification.SourceRole,
            category,
            size,
            InnolaHttp.ReadString(element, "checksum", "hash"),
            serviceReference,
            true,
            classification.SourceType);
    }

    private static string? BuildFallbackSourceFileName(JsonElement element, JsonElement? body, string? sourceId, string? sourceUid)
    {
        var extension = InnolaHttp.ReadString(element, "extension")
            ?? ReadNested(body, "extension", "Extension");
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "pdf";
        }

        var rawName = InnolaHttp.ReadString(element, "sourceNo", "documentNo", "referenceNo")
            ?? ReadNested(body, "name", "Name")
            ?? sourceUid
            ?? sourceId
            ?? "source";
        var safeName = string.Concat(rawName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? null : $"{safeName}.{extension.TrimStart('.')}";
    }

    private static bool TryParseServiceReference(string serviceReference, out string queryName, out string queryValue)
    {
        queryName = string.Empty;
        queryValue = string.Empty;
        var separator = serviceReference.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == serviceReference.Length - 1)
        {
            return false;
        }

        queryValue = serviceReference[(separator + 1)..];
        queryName = serviceReference[..separator] switch
        {
            "body-id" => "bodyId",
            "source-id" => "sourceId",
            "source-uid" => "sourceUid",
            "scan-source-id" => "scanSourceId",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(queryName);
    }

    private static AttachmentClassification ResolveAttachmentClassification(string fileName, string? category)
    {
        var text = $"{fileName} {category}".ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (TryResolveConfiguredSourceType(text, extension, out var definition))
        {
            return new AttachmentClassification(definition.SourceType, definition.WorkflowRole);
        }

        return extension is ".pdf" or ".tif" or ".tiff" or ".png" or ".jpg" or ".jpeg"
            ? new AttachmentClassification(null, SourceRole.AmbiguousDocument)
            : new AttachmentClassification(null, null);
    }

    private static bool TryResolveConfiguredSourceType(string text, string extension, out ComputeAttachmentSourceTypeDefinition definition)
    {
        definition = null!;

        var exactConfigured = ConfiguredSourceTypes.FirstOrDefault(item =>
            text.Contains(item.SourceType, StringComparison.OrdinalIgnoreCase) && item.SupportsExtension(extension));
        if (exactConfigured is not null)
        {
            definition = exactConfigured;
            return true;
        }

        string? sourceType = null;
        if (extension == ".zip" || text.Contains("sidwell-case-state", StringComparison.Ordinal) || text.Contains("resume", StringComparison.Ordinal))
        {
            sourceType = "st_survey_zip";
        }
        else if (extension == ".dwg" || text.Contains("autocad", StringComparison.Ordinal) || text.Contains("dwg", StringComparison.Ordinal) || text.Contains("cad", StringComparison.Ordinal))
        {
            sourceType = "st_autocad_file";
        }
        else if (extension is ".csv" or ".txt" || text.Contains("survey_points", StringComparison.Ordinal))
        {
            sourceType = "st_survey_points";
        }
        else if (text.Contains("surveysheet", StringComparison.Ordinal)
            || text.Contains("survey sheet", StringComparison.Ordinal)
            || text.Contains("source_computation", StringComparison.Ordinal)
            || text.Contains("computation", StringComparison.Ordinal)
            || text.Contains("comput", StringComparison.Ordinal)
            || text.Contains("comsheet", StringComparison.Ordinal)
            || text.Contains("comp sheet", StringComparison.Ordinal)
            || text.Contains("calculation", StringComparison.Ordinal)
            || text.Contains("coordinate", StringComparison.Ordinal))
        {
            sourceType = "st_surveysheet";
        }
        else if (text.Contains("surveyplan", StringComparison.Ordinal)
            || text.Contains("survey plan", StringComparison.Ordinal)
            || text.Contains("map", StringComparison.Ordinal)
            || text.Contains("geolan", StringComparison.Ordinal)
            || text.Contains("geo lan", StringComparison.Ordinal)
            || text.Contains("plan", StringComparison.Ordinal))
        {
            sourceType = "st_surveyplan";
        }

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return false;
        }

        var configured = ConfiguredSourceTypes.FirstOrDefault(item =>
            item.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase) && item.SupportsExtension(extension));
        if (configured is null)
        {
            return false;
        }

        definition = configured;
        return true;
    }

    private static JsonElement? TryObject(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;
    }

    private static string? ReadNested(JsonElement? element, params string[] names)
    {
        return element.HasValue ? InnolaHttp.ReadString(element.Value, names) : null;
    }

    private static long? ReadLong(JsonElement element, params string[] names)
    {
        var raw = InnolaHttp.ReadString(element, names);
        return long.TryParse(raw, out var value) ? value : null;
    }

    private static long? ReadNestedLong(JsonElement? element, params string[] names)
    {
        return element.HasValue ? ReadLong(element.Value, names) : null;
    }

    private sealed record AttachmentClassification(string? SourceType, string? SourceRole);
}
