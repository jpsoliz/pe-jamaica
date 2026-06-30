using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionService : IInnolaTransactionService
{
    private const int ApplicationSearchLimit = 25;
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
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                return await SearchApplicationMyTasksAsync(query, cancellationToken).ConfigureAwait(false);
            }

            var workflowResult = await GetWorkflowMyTasksAsync(query, cancellationToken).ConfigureAwait(false);
            if (!workflowResult.Success || workflowResult.Rows.Count > 0)
            {
                return workflowResult;
            }

            var searchResult = await SearchApplicationMyTasksAsync(query, cancellationToken).ConfigureAwait(false);
            return searchResult.Success ? searchResult : workflowResult;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            return InnolaTransactionListResult.Failure("Could not refresh transactions. Try again.", exception.GetType().Name);
        }
    }

    private async Task<InnolaTransactionListResult> GetWorkflowMyTasksAsync(InnolaTransactionQuery query, CancellationToken cancellationToken)
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
        return InnolaTransactionListResult.Succeeded(MapRows(responseBody, query.ProcessStep));
    }

    private async Task<InnolaTransactionListResult> SearchApplicationMyTasksAsync(InnolaTransactionQuery query, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            InnolaHttp.BuildUri(query.ServerUrl, $"{InnolaSettings.V4RestPath}application/my-tasks/search"));
        InnolaHttp.ApplyAuthHeaders(request, query.AccessToken);
        request.Content = CreateApplicationSearchContent(BuildApplicationSearchPayload(
            query,
            includeOperator: false,
            includeOrderBy: true,
            transactionSearchMode: ApplicationTransactionSearchMode.Exact));

        var result = await SendApplicationSearchAsync(request, query.ProcessStep, cancellationToken).ConfigureAwait(false);
        if (result.Success && result.Rows.Count > 0)
        {
            return result;
        }

        if (result.Success && result.Rows.Count == 0 && IsNumericSearch(query.Search))
        {
            using var wildcardRequest = new HttpRequestMessage(
                HttpMethod.Post,
                InnolaHttp.BuildUri(query.ServerUrl, $"{InnolaSettings.V4RestPath}application/my-tasks/search"));
            InnolaHttp.ApplyAuthHeaders(wildcardRequest, query.AccessToken);
            wildcardRequest.Content = CreateApplicationSearchContent(BuildApplicationSearchPayload(
                query,
                includeOperator: true,
                includeOrderBy: true,
                transactionSearchMode: ApplicationTransactionSearchMode.PrefixWildcard));

            var wildcardResult = await SendApplicationSearchAsync(wildcardRequest, query.ProcessStep, cancellationToken).ConfigureAwait(false);
            if (wildcardResult.Success && wildcardResult.Rows.Count > 0)
            {
                return wildcardResult;
            }

            result = wildcardResult.Success ? wildcardResult : result;

            if (wildcardResult.Success && wildcardResult.Rows.Count == 0 && IsNumericFragmentSearch(query.Search))
            {
                using var containsRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    InnolaHttp.BuildUri(query.ServerUrl, $"{InnolaSettings.V4RestPath}application/my-tasks/search"));
                InnolaHttp.ApplyAuthHeaders(containsRequest, query.AccessToken);
                containsRequest.Content = CreateApplicationSearchContent(BuildApplicationSearchPayload(
                    query,
                    includeOperator: true,
                    includeOrderBy: true,
                    transactionSearchMode: ApplicationTransactionSearchMode.ContainsWildcard));

                var containsResult = await SendApplicationSearchAsync(containsRequest, query.ProcessStep, cancellationToken).ConfigureAwait(false);
                if (containsResult.Success && containsResult.Rows.Count > 0)
                {
                    return containsResult;
                }

                result = containsResult.Success ? containsResult : result;
            }
        }

        if (!result.Success && !string.Equals(result.ErrorCategory, "InternalServerError", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        using var retryRequest = new HttpRequestMessage(
            HttpMethod.Post,
            InnolaHttp.BuildUri(query.ServerUrl, $"{InnolaSettings.V4RestPath}application/my-tasks/search"));
        InnolaHttp.ApplyAuthHeaders(retryRequest, query.AccessToken);
        retryRequest.Content = CreateApplicationSearchContent(BuildApplicationSearchPayload(
            query,
            includeOperator: false,
            includeOrderBy: false,
            transactionSearchMode: ApplicationTransactionSearchMode.Exact));
        return await SendApplicationSearchAsync(retryRequest, query.ProcessStep, cancellationToken).ConfigureAwait(false);
    }

    private static StringContent CreateApplicationSearchContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string BuildApplicationSearchPayload(
        InnolaTransactionQuery query,
        bool includeOperator,
        bool includeOrderBy,
        ApplicationTransactionSearchMode transactionSearchMode)
    {
        var search = query.Search?.Trim();
        var criteria = BuildSearchCriteria(search, includeOperator, transactionSearchMode);
        var payload = new ApplicationTaskSearchRequest(
            Start: 0,
            Limit: ApplicationSearchLimit,
            Text: criteria.Count == 0 ? search : null,
            Criterias: criteria,
            OrderAsc: false,
            OrderBy: includeOrderBy ? "create_time" : null,
            Page: 1);
        return JsonSerializer.Serialize(payload);
    }

    private static IReadOnlyList<ApplicationTaskSearchCriteria> BuildSearchCriteria(
        string? search,
        bool includeOperator,
        ApplicationTransactionSearchMode transactionSearchMode)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Array.Empty<ApplicationTaskSearchCriteria>();
        }

        if (IsNumericSearch(search))
        {
            var value = transactionSearchMode switch
            {
                ApplicationTransactionSearchMode.PrefixWildcard => $"{search}%",
                ApplicationTransactionSearchMode.ContainsWildcard => $"%{search}%",
                _ => search
            };
            return new[]
            {
                new ApplicationTaskSearchCriteria(
                    Field: "transaction_no",
                    Value: value,
                    Operator: includeOperator ? "ilike" : null)
            };
        }

        return Array.Empty<ApplicationTaskSearchCriteria>();
    }

    private static bool IsNumericSearch(string? search)
    {
        return !string.IsNullOrWhiteSpace(search) && search.Trim().All(char.IsDigit);
    }

    private static bool IsNumericFragmentSearch(string? search)
    {
        var trimmed = search?.Trim();
        return IsNumericSearch(trimmed) && trimmed!.Length is >= 3 and < 9;
    }

    private async Task<InnolaTransactionListResult> SendApplicationSearchAsync(
        HttpRequestMessage request,
        string processStep,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return InnolaTransactionListResult.Failure("Could not refresh transactions. Try again.", response.StatusCode.ToString());
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return InnolaTransactionListResult.Succeeded(MapRows(responseBody, processStep));
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

        foreach (var name in new[] { "value", "data", "items", "tasks", "rows", "records", "Records", "result" })
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
        var applicationId = InnolaHttp.ReadString(item, "application_id", "applicationId", "ApplicationId")
            ?? ReadNested(nestedApplication, "id", "Id");
        var taskId = InnolaHttp.ReadString(item, "task_id", "taskId", "TaskId", "workflow_task_id", "workflowTaskId", "id", "Id") ?? transactionId;
        var taskName = InnolaHttp.ReadString(item, "task_name", "taskName", "TaskName", "activity_name", "activityName", "name", "Name");

        if (string.IsNullOrWhiteSpace(transactionNumber) || string.IsNullOrWhiteSpace(taskName))
        {
            return null;
        }

        var explicitProcessStep = InnolaHttp.ReadString(item, "process_step", "processStep", "step", "workflow_step", "workflowStep");
        var processStep = string.IsNullOrWhiteSpace(explicitProcessStep) ? defaultProcessStep : explicitProcessStep;
        var statusText = InnolaHttp.ReadString(item, "status", "Status", "task_status", "taskStatus", "tr_status_text", "trStatusText")
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
            InnolaHttp.ReadString(item, "transaction_type_text", "transactionTypeText", "transaction_type", "transactionType")
                ?? ReadNested(nestedTransaction, "transactionTypeText", "transactionType", "type")
                ?? ReadNested(nestedApplication, "transactionTypeText", "transactionType", "type"),
            CleanDisplayValue(InnolaHttp.ReadString(item, "responsible_party", "responsibleParty", "requestor", "requester", "applicant", "applicant_name", "applicantName", "owner", "createdBy")),
            InnolaHttp.ReadString(item, "assigned_user", "assignedUser", "assignee_text", "assigneeText", "assignee", "Assignee", "user", "User"),
            InnolaHttp.ReadString(item, "assigned_group", "assignedGroup", "group", "Group", "groupName", "role", "roles_text", "rolesText", "roles"),
            InnolaHttp.ReadDate(item, "received_at", "receivedAt", "assigned_at", "assignedAt", "task_create_date", "taskCreateDate", "createTime", "created_at", "createdAt", "date", "Date")
                ?? ReadNestedDate(nestedTransaction, "createDatetime", "createDate")
                ?? ReadNestedDate(nestedApplication, "createDatetime", "createDate"),
            isAvailable,
            isLoadable,
            InnolaHttp.ReadString(item, "unavailable_reason", "unavailableReason", "reason", "Reason"),
            InnolaHttp.ReadString(item, "browser_url", "browserUrl", "url", "Url"),
            applicationId);
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

    private static string? CleanDisplayValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var cleaned = value.Split(new[] { "::::", ":::" }, StringSplitOptions.None)[0].Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? value.Trim() : cleaned;
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

    private sealed record ApplicationTaskSearchRequest(
        [property: JsonPropertyName("start")] int Start,
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("criterias")] IReadOnlyList<ApplicationTaskSearchCriteria> Criterias,
        [property: JsonPropertyName("orderAsc")] bool OrderAsc,
        [property: JsonPropertyName("orderBy")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? OrderBy,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("@c")] string Type = "SearchRequest");

    private sealed record ApplicationTaskSearchCriteria(
        [property: JsonPropertyName("field")] string Field,
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("operator")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Operator);

    private enum ApplicationTransactionSearchMode
    {
        Exact,
        PrefixWildcard,
        ContainsWildcard
    }
}
