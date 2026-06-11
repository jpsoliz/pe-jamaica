using System.Net.Http;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionService : IInnolaTransactionService
{
    private readonly HttpClient httpClient;

    public InnolaTransactionService()
        : this(new HttpClient())
    {
    }

    public InnolaTransactionService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.ServerUrl) || string.IsNullOrWhiteSpace(query.AccessToken))
        {
            return InnolaTransactionListResult.Failure("Could not refresh transactions. Try again.", "unauthorized");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                InnolaHttp.BuildUri(query.ServerUrl, $"{InnolaSettings.V4RestPath}workflow/my-tasks"));
            InnolaHttp.ApplyAuthHeaders(request, query.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return InnolaTransactionListResult.Failure("Could not refresh transactions. Try again.", response.StatusCode.ToString());
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var rows = MapRows(responseBody, query.ProcessStep);
            return InnolaTransactionListResult.Succeeded(rows);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            return InnolaTransactionListResult.Failure("Could not refresh transactions. Try again.", exception.GetType().Name);
        }
    }

    public static IReadOnlyList<InnolaTransactionRow> MapRows(string responseBody, string processStep)
    {
        using var document = JsonDocument.Parse(responseBody);
        var candidates = FindTaskArray(document.RootElement);
        var rows = candidates.Select(item => MapRow(item, processStep)).Where(row => row is not null).Cast<InnolaTransactionRow>();
        return InnolaTransactionFiltering.FilterAvailableRows(rows, processStep);
    }

    private static IEnumerable<JsonElement> FindTaskArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().ToArray();
        }

        foreach (var name in new[] { "value", "data", "items", "tasks", "rows", "result" })
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().ToArray();
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                var nested = FindTaskArray(value).ToArray();
                if (nested.Length > 0)
                {
                    return nested;
                }
            }
        }

        return Array.Empty<JsonElement>();
    }

    private static InnolaTransactionRow? MapRow(JsonElement item, string defaultProcessStep)
    {
        var nestedTransaction = TryObject(item, "transaction");
        var nestedApplication = TryObject(item, "application");

        var transactionNumber = ReadNested(nestedTransaction, "transaction_no", "transactionNo", "transactionNumber", "TransactionNo", "TransactionNumber")
            ?? InnolaHttp.ReadString(item, "transaction_no", "transactionNo", "transactionNumber", "TransactionNo", "TransactionNumber")
            ?? ReadNested(nestedApplication, "applicationNo", "application_no");
        var transactionId = InnolaHttp.ReadString(item, "transaction_id", "transactionId", "TransactionId")
            ?? ReadNested(nestedTransaction, "id", "Id")
            ?? transactionNumber;
        var taskId = InnolaHttp.ReadString(item, "task_id", "taskId", "TaskId", "workflow_task_id", "workflowTaskId", "id", "Id") ?? transactionId;
        var taskName = InnolaHttp.ReadString(item, "task_name", "taskName", "TaskName", "activity_name", "activityName", "name", "Name");

        if (string.IsNullOrWhiteSpace(transactionNumber) || string.IsNullOrWhiteSpace(taskName))
        {
            return null;
        }

        var explicitProcessStep = InnolaHttp.ReadString(item, "process_step", "processStep", "step", "workflow_step", "workflowStep");
        var processStep = string.IsNullOrWhiteSpace(explicitProcessStep) ? defaultProcessStep : explicitProcessStep;
        var statusText = InnolaHttp.ReadString(item, "status", "Status", "task_status", "taskStatus")
            ?? ReadNested(nestedTransaction, "status", "Status");
        var status = ParseStatus(statusText);
        var isAvailable = ReadBool(item, true, "is_available", "isAvailable", "available", "Available");
        var isLoadable = ReadBool(item, true, "is_loadable", "isLoadable", "doable", "isTaskDoable", "canLoad");

        return new InnolaTransactionRow(
            taskId ?? string.Empty,
            transactionId ?? transactionNumber,
            transactionNumber,
            taskName,
            processStep,
            status,
            InnolaHttp.ReadString(item, "responsible_party", "responsibleParty", "requestor", "requester", "applicant", "owner", "createdBy"),
            InnolaHttp.ReadString(item, "assigned_user", "assignedUser", "assignee", "Assignee", "user", "User"),
            InnolaHttp.ReadString(item, "assigned_group", "assignedGroup", "group", "Group", "groupName", "role"),
            InnolaHttp.ReadDate(item, "received_at", "receivedAt", "assigned_at", "assignedAt", "createTime", "created_at", "createdAt", "date", "Date")
                ?? ReadNestedDate(nestedTransaction, "createDatetime", "createDate")
                ?? ReadNestedDate(nestedApplication, "createDatetime", "createDate"),
            isAvailable,
            isLoadable,
            InnolaHttp.ReadString(item, "unavailable_reason", "unavailableReason", "reason", "Reason"),
            InnolaHttp.ReadString(item, "browser_url", "browserUrl", "url", "Url"));
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

    private static DateTimeOffset? ReadNestedDate(JsonElement? element, params string[] names)
    {
        return element.HasValue ? InnolaHttp.ReadDate(element.Value, names) : null;
    }

    private static bool ReadBool(JsonElement element, bool defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static InnolaTransactionStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return InnolaTransactionStatus.Available;
        }

        var normalized = status.Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("done", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            return InnolaTransactionStatus.Completed;
        }

        if (normalized.Equals("inprogress", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("started", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("processing", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("procstatusprocessing", StringComparison.OrdinalIgnoreCase))
        {
            return InnolaTransactionStatus.InProgress;
        }

        if (normalized.Equals("locked", StringComparison.OrdinalIgnoreCase))
        {
            return InnolaTransactionStatus.Locked;
        }

        if (normalized.Equals("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return InnolaTransactionStatus.Unavailable;
        }

        return InnolaTransactionStatus.Available;
    }
}
