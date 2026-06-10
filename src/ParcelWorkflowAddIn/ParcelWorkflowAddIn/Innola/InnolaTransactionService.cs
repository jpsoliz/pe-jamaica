using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var requestUri = new Uri(new Uri(NormalizeServerUrl(query.ServerUrl)), $"{InnolaSettings.RestPath.TrimStart('/')}application/getmytasks");
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(new GetMyTasksRequest(
                    query.Username,
                    query.Groups,
                    query.ProcessStep,
                    query.Filter,
                    query.Search,
                    query.SortField,
                    query.SortDirection))
            };
            request.Headers.TryAddWithoutValidation("Access-Token", query.AccessToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", query.AccessToken);

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
        var transactionNumber = ReadString(item, "transaction_no", "transactionNo", "transactionNumber", "TransactionNo", "TransactionNumber");
        var transactionId = ReadString(item, "transaction_id", "transactionId", "TransactionId", "id", "Id") ?? transactionNumber;
        var taskId = ReadString(item, "task_id", "taskId", "TaskId", "workflow_task_id", "workflowTaskId") ?? transactionId;
        var taskName = ReadString(item, "task_name", "taskName", "TaskName", "activity_name", "activityName", "name", "Name");

        if (string.IsNullOrWhiteSpace(transactionNumber) || string.IsNullOrWhiteSpace(taskName))
        {
            return null;
        }

        var processStep = ReadString(item, "process_step", "processStep", "step", "workflow_step", "workflowStep") ?? defaultProcessStep;
        var statusText = ReadString(item, "status", "Status", "task_status", "taskStatus");
        var status = ParseStatus(statusText);
        var isAvailable = ReadBool(item, true, "is_available", "isAvailable", "available", "Available");
        var isLoadable = ReadBool(item, true, "is_loadable", "isLoadable", "doable", "isTaskDoable", "canLoad");
        var unavailableReason = ReadString(item, "unavailable_reason", "unavailableReason", "reason", "Reason");

        return new InnolaTransactionRow(
            taskId ?? string.Empty,
            transactionId ?? transactionNumber,
            transactionNumber,
            taskName,
            processStep,
            status,
            ReadString(item, "responsible_party", "responsibleParty", "requestor", "requester", "applicant", "owner", "createdBy"),
            ReadString(item, "assigned_user", "assignedUser", "assignee", "Assignee", "user", "User"),
            ReadString(item, "assigned_group", "assignedGroup", "group", "Group", "groupName"),
            ReadDate(item, "received_at", "receivedAt", "assigned_at", "assignedAt", "created_at", "createdAt", "date", "Date"),
            isAvailable,
            isLoadable,
            unavailableReason,
            ReadString(item, "browser_url", "browserUrl", "url", "Url"));
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        var trimmed = serverUrl.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return value.ToString();
                }
            }
        }

        return null;
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

    private static DateTimeOffset? ReadDate(JsonElement element, params string[] names)
    {
        var raw = ReadString(element, names);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return null;
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
            || normalized.Equals("started", StringComparison.OrdinalIgnoreCase))
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

    private sealed record GetMyTasksRequest(
        [property: JsonPropertyName("user")] string User,
        [property: JsonPropertyName("groups")] IReadOnlyList<string> Groups,
        [property: JsonPropertyName("process_step")] string ProcessStep,
        [property: JsonPropertyName("filter")] string? Filter,
        [property: JsonPropertyName("search")] string? Search,
        [property: JsonPropertyName("sort_field")] string? SortField,
        [property: JsonPropertyName("sort_direction")] string? SortDirection);
}
