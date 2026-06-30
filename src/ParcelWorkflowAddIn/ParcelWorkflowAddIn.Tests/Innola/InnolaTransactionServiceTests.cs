using ParcelWorkflowAddIn.Innola;
using System.Net;
using System.Text;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionServiceTests
{
    public static void MapsPriorInnolaTaskPayloadAndFiltersUnavailableRows()
    {
        var rows = InnolaTransactionService.MapRows("""
            {
              "value": [
                {
                  "task_id": "task-1",
                  "transaction_id": "100000004",
                  "transaction_no": "TR100000004",
                  "task_name": "Computation Check",
                  "process_step": "parcel_workflow",
                  "status": "Available",
                  "assigned_user": "tester",
                  "assigned_group": "survey",
                  "requestor": "Alex Robinson",
                  "assigned_at": "2024-10-15T09:24:00-05:00",
                  "browser_url": "https://example/tasks/1"
                },
                {
                  "task_id": "task-2",
                  "transaction_id": "100000005",
                  "transaction_no": "TR100000005",
                  "task_name": "Completed Task",
                  "process_step": "parcel_workflow",
                  "status": "Completed",
                  "assigned_user": "tester"
                },
                {
                  "task_id": "task-3",
                  "transaction_id": "100000006",
                  "transaction_no": "TR100000006",
                  "task_name": "Wrong Step Task",
                  "process_step": "post_registration",
                  "status": "Available",
                  "assigned_user": "tester"
                }
              ]
            }
            """, "parcel_workflow");

        TestAssert.Equal(1, rows.Count, "Only available parcel workflow rows should remain.");
        var row = rows[0];
        TestAssert.Equal("task-1", row.TaskId, "Task id mismatch.");
        TestAssert.Equal("100000004", row.TransactionId, "Transaction id mismatch.");
        TestAssert.Equal("TR100000004", row.TransactionNumber, "Transaction number mismatch.");
        TestAssert.Equal("Computation Check", row.TaskName, "Task name mismatch.");
        TestAssert.Equal("Alex Robinson", row.ResponsibleParty, "Responsible party mismatch.");
        TestAssert.Equal("tester", row.AssignedUser, "Assigned user mismatch.");
        TestAssert.Equal("survey", row.AssignedGroup, "Assigned group mismatch.");
        TestAssert.Equal("https://example/tasks/1", row.BrowserUrl, "Browser URL mismatch.");
        TestAssert.Equal(InnolaTransactionStatus.Available, row.Status, "Status mismatch.");
        TestAssert.True(row.IsLoadable, "Mapped row should be loadable.");
    }

    public static async Task HttpTransactionServiceUsesWorkflowMyTasksEndpoint()
    {
        var handler = new CapturingHttpMessageHandler("""
            [
              {
                "id": "task-1",
                "name": "Computation Check",
                "assignee": "tester",
                "role": "ROLE_Survey",
                "createTime": "2024-10-15T09:24:00-05:00",
                "taskKey": "task_enterdata",
                "transactionId": "100000004",
                "transaction": {
                  "id": "100000004",
                  "transactionNo": "TR100000004",
                  "status": "proc_status_created"
                }
              }
            ]
            """);
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            "token-abc",
            "tester",
            new[] { "survey", "qc" },
            "parcel_workflow",
            "All tasks",
            "",
            "Transaction no",
            "Ascending"));

        TestAssert.True(result.Success, "HTTP service should map success response.");
        TestAssert.Equal(1, result.Rows.Count, "Row count mismatch.");
        TestAssert.Equal(HttpMethod.Get, handler.LastMethod, "Task list should use GET.");
        TestAssert.True(handler.LastUri?.AbsoluteUri.EndsWith("/api/v4/rest/workflow/my-tasks", StringComparison.Ordinal) ?? false, "Task endpoint mismatch.");
        TestAssert.True(handler.LastAccessToken == "token-abc", "Access-Token header mismatch.");
        TestAssert.True(!handler.HasAuthorizationHeader, "Innola requests should use Access-Token header only.");
        TestAssert.Equal(string.Empty, handler.LastRequestBody, "Workflow my-tasks should not send the search request body.");
        TestAssert.Equal("task-1", result.Rows[0].TaskId, "Task id mismatch.");
        TestAssert.Equal("TR100000004", result.Rows[0].TransactionNumber, "Nested transaction number mismatch.");
        TestAssert.Equal("ROLE_Survey", result.Rows[0].AssignedGroup, "Role should map as assigned group.");
    }

    public static async Task HttpTransactionServiceFallsBackToApplicationSearchWhenWorkflowRowsAreEmpty()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, "[]"),
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "success": true,
                  "total": 70,
                  "records": [
                    {
                      "id": "task-search-1",
                      "transaction_id": "019e-task",
                      "application_id": "019e-app",
                      "transaction_no": "100000206",
                      "name": "Assign Computation Task",
                      "transaction_type_text": "Plan Examination",
                      "applicant": "Doe, Jhon F::::019eb89a-3745-7313-b7c3-2410583e9bb4::::019eb89a-3744-713f-b43b-bc47e9a32f5d",
                      "roles_text": "Plan Reviewer (Computation)",
                      "assignee": "jpablo",
                      "assignee_text": "Juan Pablo",
                      "task_create_date": "2026-06-11T14:19:00-05:00",
                      "tr_status_text": "Processing"
                    }
                  ]
                }
                """));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs-dev.innola-solutions.com/",
            "token-abc",
            "jpablo",
            new[] { "Super Group" },
            "parcel_workflow",
            "All tasks",
            null,
            "Received",
            "Descending"));

        TestAssert.True(result.Success, "Fallback search should return a successful list.");
        TestAssert.Equal(1, result.Rows.Count, "Search fallback row count mismatch.");
        TestAssert.Equal(2, handler.Requests.Count, "Fallback should issue workflow GET and application search POST.");
        TestAssert.Equal(HttpMethod.Get, handler.Requests[0].Method, "First request should remain workflow GET.");
        TestAssert.True(handler.Requests[0].Uri.AbsoluteUri.EndsWith("/api/v4/rest/workflow/my-tasks", StringComparison.Ordinal), "Workflow endpoint mismatch.");
        TestAssert.Equal(HttpMethod.Post, handler.Requests[1].Method, "Second request should be search POST.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.EndsWith("/api/v4/rest/application/my-tasks/search", StringComparison.Ordinal), "Search endpoint mismatch.");
        TestAssert.True(handler.Requests[1].Body.Contains("\"limit\":25", StringComparison.Ordinal), "Search body should include the expected page limit.");
        TestAssert.True(handler.Requests[1].Body.Contains("\"orderBy\":\"create_time\"", StringComparison.Ordinal), "Search body should match the Innola task search order field.");

        var row = result.Rows[0];
        TestAssert.Equal("task-search-1", row.TaskId, "Search task id mismatch.");
        TestAssert.Equal("019e-task", row.TransactionId, "Search transaction id mismatch.");
        TestAssert.Equal("019e-app", row.ApplicationId, "Search application id mismatch.");
        TestAssert.Equal("100000206", row.TransactionNumber, "Search transaction number mismatch.");
        TestAssert.Equal("Assign Computation Task", row.TaskName, "Search task name mismatch.");
        TestAssert.Equal("Doe, Jhon F", row.ResponsibleParty, "Applicant display value should remove Innola id suffixes.");
        TestAssert.Equal("Juan Pablo", row.AssignedUser, "Search assignee display name mismatch.");
        TestAssert.Equal("Plan Reviewer (Computation)", row.AssignedGroup, "Search role display name mismatch.");
        TestAssert.Equal(InnolaTransactionStatus.InProgress, row.Status, "Search status mismatch.");
    }

    public static async Task HttpTransactionServiceKeepsWorkflowResultWhenApplicationSearchFails()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, "[]"),
            new SequencedResponse(HttpStatusCode.InternalServerError, "{}"),
            new SequencedResponse(HttpStatusCode.InternalServerError, "{}"));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs-dev.innola-solutions.com/",
            "token-abc",
            "jpablo",
            new[] { "Super Group" },
            "parcel_workflow",
            "All tasks",
            null,
            "Received",
            "Descending"));

        TestAssert.True(result.Success, "A failing search fallback should not turn an empty workflow result into a hard refresh failure.");
        TestAssert.Equal(0, result.Rows.Count, "Workflow empty result should remain empty.");
        TestAssert.Equal(3, handler.Requests.Count, "InternalServerError should trigger one minimal search retry.");
        TestAssert.True(handler.Requests[2].Body.Contains("\"limit\":25", StringComparison.Ordinal), "Retry search body should include limit.");
        TestAssert.True(!handler.Requests[2].Body.Contains("orderBy", StringComparison.Ordinal), "Retry search body should remove orderBy.");
    }

    public static async Task HttpTransactionServiceUsesExactTransactionNumberSearchPayload()
    {
        var handler = new CapturingHttpMessageHandler("""
            {
              "records": [
                {
                  "id": "task-100000400",
                  "transaction_no": "100000400",
                  "name": "Compute Survey Plan",
                  "transaction_type_text": "Plan Examination",
                  "assignee": "jpablo",
                  "tr_status_text": "Processing"
                }
              ],
              "allowRead": true,
              "allowWrite": true
            }
            """);
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs-dev.innola-solutions.com/",
            "token-abc",
            "jpablo",
            new[] { "Super Group" },
            "parcel_workflow",
            "All tasks",
            "100000400",
            "Received",
            "Descending"));

        TestAssert.True(result.Success, "Exact transaction search should return a successful list.");
        TestAssert.Equal(1, result.Rows.Count, "Exact transaction search row count mismatch.");
        TestAssert.Equal(HttpMethod.Post, handler.LastMethod, "Exact transaction search should use application search POST.");
        TestAssert.True(handler.LastUri?.AbsoluteUri.EndsWith("/api/v4/rest/application/my-tasks/search", StringComparison.Ordinal) ?? false, "Search endpoint mismatch.");
        TestAssert.True(handler.LastRequestBody.Contains("\"@c\":\"SearchRequest\"", StringComparison.Ordinal), "Search body should declare SearchRequest.");
        TestAssert.True(handler.LastRequestBody.Contains("\"field\":\"transaction_no\"", StringComparison.Ordinal), "Search body should target transaction_no.");
        TestAssert.True(handler.LastRequestBody.Contains("\"value\":\"100000400\"", StringComparison.Ordinal), "Search body should use the exact transaction number.");
        TestAssert.True(!handler.LastRequestBody.Contains("\"operator\"", StringComparison.Ordinal), "Exact transaction search should not send an operator.");
        TestAssert.True(handler.LastRequestBody.Contains("\"limit\":25", StringComparison.Ordinal), "Search body should use Innola's expected limit.");
        TestAssert.True(handler.LastRequestBody.Contains("\"orderBy\":\"create_time\"", StringComparison.Ordinal), "Search body should use create_time ordering.");
        TestAssert.Equal("100000400", result.Rows[0].TransactionNumber, "Exact transaction result mismatch.");
        TestAssert.Equal("Compute Survey Plan", result.Rows[0].TaskName, "Exact transaction task name mismatch.");
        TestAssert.Equal(InnolaTransactionStatus.InProgress, result.Rows[0].Status, "Exact transaction status mismatch.");
    }

    public static async Task HttpTransactionServiceFallsBackToWildcardTransactionSearchWhenExactReturnsEmpty()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, """{ "records": [] }"""),
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "records": [
                    {
                      "id": "task-100000400",
                      "transaction_no": "100000400",
                      "name": "Compute Survey Plan",
                      "transaction_type_text": "Plan Examination",
                      "assignee": "jpablo",
                      "tr_status_text": "Processing"
                    }
                  ]
                }
                """));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs-dev.innola-solutions.com/",
            "token-abc",
            "jpablo",
            new[] { "Super Group" },
            "parcel_workflow",
            "All tasks",
            "100000400",
            "Received",
            "Descending"));

        TestAssert.True(result.Success, "Wildcard transaction fallback should return a successful list.");
        TestAssert.Equal(1, result.Rows.Count, "Wildcard transaction fallback row count mismatch.");
        TestAssert.Equal(2, handler.Requests.Count, "Empty exact search should trigger one wildcard search.");
        TestAssert.True(handler.Requests[0].Body.Contains("\"value\":\"100000400\"", StringComparison.Ordinal), "First search should use exact transaction number.");
        TestAssert.True(!handler.Requests[0].Body.Contains("\"operator\"", StringComparison.Ordinal), "First search should not send an operator.");
        TestAssert.True(handler.Requests[1].Body.Contains("\"value\":\"100000400%\"", StringComparison.Ordinal), "Wildcard search should use transaction prefix wildcard.");
        TestAssert.True(handler.Requests[1].Body.Contains("\"operator\":\"ilike\"", StringComparison.Ordinal), "Wildcard search should use lowercase ilike operator.");
        TestAssert.Equal("100000400", result.Rows[0].TransactionNumber, "Wildcard transaction result mismatch.");
    }

    public static async Task HttpTransactionServiceFallsBackToContainsWildcardForShortTransactionFragments()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, """{ "records": [] }"""),
            new SequencedResponse(HttpStatusCode.OK, """{ "records": [] }"""),
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "records": [
                    {
                      "id": "task-100000379",
                      "transaction_no": "100000379",
                      "name": "Compute Survey Plan",
                      "transaction_type_text": "Plan Examination",
                      "assignee": "jpablo",
                      "tr_status_text": "Processing"
                    }
                  ]
                }
                """));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs-dev.innola-solutions.com/",
            "token-abc",
            "jpablo",
            new[] { "Super Group" },
            "parcel_workflow",
            "All tasks",
            "379",
            "Received",
            "Descending"));

        TestAssert.True(result.Success, "Short transaction fragment fallback should return a successful list.");
        TestAssert.Equal(1, result.Rows.Count, "Short transaction fragment fallback row count mismatch.");
        TestAssert.Equal(3, handler.Requests.Count, "Short numeric fragments should try exact, prefix wildcard, then contains wildcard.");
        TestAssert.True(handler.Requests[0].Body.Contains("\"value\":\"379\"", StringComparison.Ordinal), "First search should use the exact fragment.");
        TestAssert.True(handler.Requests[1].Body.Contains("\"value\":\"379%\"", StringComparison.Ordinal), "Second search should use prefix wildcard.");
        TestAssert.True(handler.Requests[2].Body.Contains("\"value\":\"%379%\"", StringComparison.Ordinal), "Third search should use contains wildcard.");
        TestAssert.True(handler.Requests[2].Body.Contains("\"operator\":\"ilike\"", StringComparison.Ordinal), "Contains wildcard search should use lowercase ilike operator.");
        TestAssert.Equal("100000379", result.Rows[0].TransactionNumber, "Short transaction fragment result mismatch.");
    }

    public static async Task MockTransactionServiceRequiresSessionAndFiltersRows()
    {
        var service = new MockInnolaTransactionService();

        var unauthorized = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            string.Empty,
            "tester",
            Array.Empty<string>(),
            "parcel_workflow",
            null,
            null,
            null,
            null));

        TestAssert.True(!unauthorized.Success, "Mock service should still require a session token.");

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            "token-abc",
            "tester",
            new[] { "survey" },
            "parcel_workflow",
            null,
            null,
            null,
            null));

        TestAssert.True(result.Success, "Mock service should return rows for logged-in sessions.");
        TestAssert.True(result.Rows.Count >= 6, "Mock service should include sample task rows.");
        TestAssert.True(result.Rows.All(row => row.ProcessStep == "parcel_workflow"), "Mock service should filter wrong-step rows.");
        TestAssert.True(result.Rows.All(row => row.Status != InnolaTransactionStatus.Completed), "Mock service should filter completed rows.");
        TestAssert.True(result.Rows.Any(row => row.TransactionNumber == "TR100000004"), "Mock rows should include computation check sample.");
    }

    public static async Task TransactionErrorRedactsSecrets()
    {
        var result = InnolaTransactionListResult.Failure("token secret-password { raw request } at Stack.Trace", "bad");

        TestAssert.Equal("Could not refresh transactions. Try again.", result.ErrorMessage, "Secret-like error should be redacted.");
        TestAssert.True(!result.ErrorMessage!.Contains("secret-password", StringComparison.Ordinal), "Password should not leak.");
        TestAssert.True(!result.ErrorMessage.Contains("token", StringComparison.OrdinalIgnoreCase), "Token should not leak.");

        var handler = new CapturingHttpMessageHandler("{}", HttpStatusCode.Unauthorized);
        var service = new InnolaTransactionService(new HttpClient(handler));
        var httpResult = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            "token-abc",
            "tester",
            Array.Empty<string>(),
            "parcel_workflow",
            null,
            null,
            null,
            null));

        TestAssert.True(!httpResult.Success, "Unauthorized HTTP response should fail.");
        TestAssert.Equal("Could not refresh transactions. Try again.", httpResult.ErrorMessage, "HTTP failure message mismatch.");
        TestAssert.True(!httpResult.ErrorMessage!.Contains("token-abc", StringComparison.Ordinal), "HTTP failure must not expose token.");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string responseBody;
        private readonly HttpStatusCode statusCode;

        public CapturingHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            this.responseBody = responseBody;
            this.statusCode = statusCode;
        }

        public Uri? LastUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastAccessToken { get; private set; }

        public bool HasAuthorizationHeader { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastAccessToken = request.Headers.TryGetValues("Access-Token", out var values)
                ? values.FirstOrDefault()
                : null;
            HasAuthorizationHeader = request.Headers.Authorization is not null;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<SequencedResponse> responses;

        public SequencedHttpMessageHandler(params SequencedResponse[] responses)
        {
            this.responses = new Queue<SequencedResponse>(responses);
        }

        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));

            var response = responses.Dequeue();
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed record SequencedResponse(HttpStatusCode StatusCode, string Body);
}
