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
            "TR100",
            "Transaction no",
            "Ascending"));

        TestAssert.True(result.Success, "HTTP service should map success response.");
        TestAssert.Equal(1, result.Rows.Count, "Row count mismatch.");
        TestAssert.Equal(HttpMethod.Get, handler.LastMethod, "Task list should use GET.");
        TestAssert.True(handler.LastUri?.AbsoluteUri.EndsWith("/api/v4/rest/workflow/my-tasks", StringComparison.Ordinal) ?? false, "Task endpoint mismatch.");
        TestAssert.True(handler.LastAccessToken == "token-abc", "Access-Token header mismatch.");
        TestAssert.Equal(string.Empty, handler.LastRequestBody, "Workflow my-tasks should not send the legacy request body.");
        TestAssert.Equal("task-1", result.Rows[0].TaskId, "Task id mismatch.");
        TestAssert.Equal("TR100000004", result.Rows[0].TransactionNumber, "Nested transaction number mismatch.");
        TestAssert.Equal("ROLE_Survey", result.Rows[0].AssignedGroup, "Role should map as assigned group.");
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

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastAccessToken = request.Headers.TryGetValues("Access-Token", out var values)
                ? values.FirstOrDefault()
                : null;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
