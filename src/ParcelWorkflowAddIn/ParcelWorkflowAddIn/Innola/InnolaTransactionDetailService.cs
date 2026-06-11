using System.IO;
using System.Net.Http;
using System.Text.Json;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionDetailService : IInnolaTransactionDetailService
{
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
                return InnolaTransactionDetailResult.Failure("Could not load transaction. Try again.", response.StatusCode.ToString());
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseBody);
            var taskRoot = ResolveTaskRoot(document.RootElement);
            if (!taskRoot.HasValue)
            {
                return InnolaTransactionDetailResult.Failure("Transaction details were not found.", "not_found");
            }

            var detail = MapDetail(taskRoot.Value, selectedTransaction);
            if (detail.Attachments.Count == 0)
            {
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
            var uri = InnolaHttp.BuildUri(
                session.ServerUrl,
                $"{InnolaSettings.V4RestPath}source/download?{queryName}={Uri.EscapeDataString(queryValue)}&attachment=false&documentName={Uri.EscapeDataString(attachment.FileName)}");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            InnolaHttp.ApplyAuthHeaders(request, session.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return InnolaAttachmentContentResult.Failure("Could not load attachment. Try again.", response.StatusCode.ToString());
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return content.Length == 0
                ? InnolaAttachmentContentResult.Failure("Attachment content was not found.", "not_found")
                : InnolaAttachmentContentResult.Succeeded(content);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            return InnolaAttachmentContentResult.Failure("Could not load attachment. Try again.", exception.GetType().Name);
        }
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
            ?? ReadNested(application, "applicationNo", "application_no")
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
            if (IsAttachmentContainer(parentName) && TryMapAttachment(element) is { } attachment)
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

    private static InnolaAttachmentMetadata? TryMapAttachment(JsonElement element)
    {
        var fileName = InnolaHttp.ReadString(element, "fileName", "filename", "name", "documentName", "originalName");
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            return null;
        }

        var bodyId = InnolaHttp.ReadString(element, "bodyId", "fileBodyId");
        var sourceUid = InnolaHttp.ReadString(element, "sourceUid", "uid");
        var sourceId = InnolaHttp.ReadString(element, "sourceId", "id");
        var serviceReference = !string.IsNullOrWhiteSpace(bodyId)
            ? $"body-id:{bodyId}"
            : !string.IsNullOrWhiteSpace(sourceUid)
                ? $"source-uid:{sourceUid}"
                : !string.IsNullOrWhiteSpace(sourceId)
                    ? $"source-id:{sourceId}"
                    : null;
        if (string.IsNullOrWhiteSpace(serviceReference))
        {
            return null;
        }

        var category = InnolaHttp.ReadString(element, "category", "type", "sourceType", "documentType");
        return new InnolaAttachmentMetadata(
            serviceReference,
            fileName,
            Path.GetExtension(fileName).ToLowerInvariant(),
            InnolaHttp.ReadString(element, "mimeType", "contentType"),
            InferSourceRole(fileName, category),
            category,
            ReadLong(element, "size", "fileSize", "length"),
            InnolaHttp.ReadString(element, "checksum", "hash"),
            serviceReference,
            true);
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
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(queryName);
    }

    private static string? InferSourceRole(string fileName, string? category)
    {
        var text = $"{fileName} {category}".ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".dwg" || text.Contains("dwg", StringComparison.Ordinal))
        {
            return SourceRole.DwgReference;
        }

        if (extension is ".csv" or ".txt" || text.Contains("point", StringComparison.Ordinal))
        {
            return SourceRole.PointsComputation;
        }

        if (text.Contains("plan", StringComparison.Ordinal) || text.Contains("map", StringComparison.Ordinal))
        {
            return SourceRole.PlanMapReference;
        }

        if (text.Contains("comput", StringComparison.Ordinal))
        {
            return SourceRole.ComputationSource;
        }

        return extension is ".pdf" or ".tif" or ".tiff" or ".png" or ".jpg" or ".jpeg"
            ? SourceRole.AmbiguousDocument
            : null;
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
}
