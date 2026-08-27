using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Reports;
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
                  "owner": "Estate of Henry Brown",
                  "surveyor": "Mary Blake",
                  "parish": "St. Ann",
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
        TestAssert.Equal("Alex Robinson", row.Applicant, "Applicant mismatch.");
        TestAssert.Equal("Estate of Henry Brown", row.OwnerOrResponsibleParty, "Owner/responsible mismatch.");
        TestAssert.Equal("Mary Blake", row.Surveyor, "Surveyor mismatch.");
        TestAssert.Equal("St. Ann", row.Parish, "Parish mismatch.");
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
            new SequencedResponse(HttpStatusCode.InternalServerError, "{}"),
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
        TestAssert.Equal(5, handler.Requests.Count, "InternalServerError should trigger resilience retries and one minimal search retry path.");
        TestAssert.True(handler.Requests[3].Body.Contains("\"limit\":25", StringComparison.Ordinal), "Retry search body should include limit.");
        TestAssert.True(!handler.Requests[3].Body.Contains("orderBy", StringComparison.Ordinal), "Retry search body should remove orderBy.");
    }

    public static async Task HttpTransactionServiceRetriesTransientWorkflowStatus()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.ServiceUnavailable, "{}"),
            new SequencedResponse(HttpStatusCode.OK, """
                [
                  {
                    "id": "task-1",
                    "name": "Computation Check",
                    "assignee": "tester",
                    "taskKey": "task_enterdata",
                    "transactionId": "100000004",
                    "transaction": {
                      "transactionNo": "TR100000004"
                    }
                  }
                ]
                """));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            "token-abc",
            "tester",
            new[] { "survey" },
            "parcel_workflow",
            "All tasks",
            "",
            "Transaction no",
            "Ascending"));

        TestAssert.True(result.Success, "Transient 503 should be retried.");
        TestAssert.Equal(1, result.Rows.Count, "Retried workflow response should map rows.");
        TestAssert.Equal(2, handler.Requests.Count, "One 503 should produce one automatic retry.");
    }

    public static async Task HttpTransactionServiceRetriesDroppedWorkflowConnection()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(new HttpRequestException("connection reset")),
            new SequencedResponse(HttpStatusCode.OK, """
                [
                  {
                    "id": "task-1",
                    "name": "Computation Check",
                    "assignee": "tester",
                    "taskKey": "task_enterdata",
                    "transactionId": "100000004",
                    "transaction": {
                      "transactionNo": "TR100000004"
                    }
                  }
                ]
                """));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            "token-abc",
            "tester",
            new[] { "survey" },
            "parcel_workflow",
            "All tasks",
            "",
            "Transaction no",
            "Ascending"));

        TestAssert.True(result.Success, "Dropped connection should be retried.");
        TestAssert.Equal(2, handler.Requests.Count, "Connection failure should produce one automatic retry.");
    }

    public static async Task HttpTransactionServiceAuthFailureRequestsLoginAgain()
    {
        var handler = new SequencedHttpMessageHandler(new SequencedResponse(HttpStatusCode.Unauthorized, "{}"));
        var service = new InnolaTransactionService(new HttpClient(handler));

        var result = await service.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            "https://eltrs.innola-solutions.com/",
            "token-abc",
            "tester",
            new[] { "survey" },
            "parcel_workflow",
            "All tasks",
            "",
            "Transaction no",
            "Ascending"));

        TestAssert.True(!result.Success, "Unauthorized response should fail.");
        TestAssert.Equal("session_expired", result.ErrorCategory, "Unauthorized response should be classified as session expiration.");
        TestAssert.Equal("Innola connection could not be restored. Please log in again and retry.", result.ErrorMessage, "Auth failure should request login again.");
        TestAssert.True(!result.ErrorMessage!.Contains("token-abc", StringComparison.Ordinal), "Auth failure must not expose token.");
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
        TestAssert.Equal("Innola connection could not be restored. Please log in again and retry.", httpResult.ErrorMessage, "HTTP failure message mismatch.");
        TestAssert.True(!httpResult.ErrorMessage!.Contains("token-abc", StringComparison.Ordinal), "HTTP failure must not expose token.");
    }

    public static async Task AttachmentUploadRegistersCompareReportSourceType()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "@id": 7,
                  "type": "uploaded_placeholder",
                  "body": { "@id": 8 },
                  "link": { "@id": 9 }
                }
                """),
            new SequencedResponse(HttpStatusCode.OK, "[]"),
            new SequencedResponse(HttpStatusCode.OK, "[]"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "jpablo",
            "secret-password",
            "token-abc",
            new InnolaUserContext("jpablo", "Juan Pablo", new[] { "Super Group" }, Array.Empty<string>()),
            null);
        var transaction = new SelectedInnolaTransaction(
            "task-1",
            "transaction-1",
            "TR100000674",
            "Compare Survey Plan",
            "Compare",
            DateTimeOffset.Parse("2026-07-22T00:00:00Z"));

        var result = await service.UploadAttachmentAsync(
            session,
            transaction,
            "compare_review_report.pdf",
            "application/pdf",
            Encoding.UTF8.GetBytes("%PDF-1.4 test"),
            "st_compare_report");

        TestAssert.True(result.Success, "Compare report upload and registration should succeed.");
        TestAssert.Equal(3, handler.Requests.Count, "Upload should attach the file, load current sources, then register the source list.");
        TestAssert.True(handler.Requests[0].Uri.AbsoluteUri.Contains("sourceType=st_compare_report", StringComparison.Ordinal), "Attachment upload query should use st_compare_report.");
        TestAssert.True(handler.Requests[2].Uri.AbsoluteUri.Contains("typeKeyId=source", StringComparison.Ordinal), "Final request should register administrative sources.");
        TestAssert.True(handler.Requests[2].Body.Contains("\"type\":\"st_compare_report\"", StringComparison.Ordinal), "Registered source payload should preserve st_compare_report.");
        TestAssert.True(!handler.Requests[2].Body.Contains("\"type\":\"st_surveyplan\"", StringComparison.Ordinal), "Compare report must not be rewritten to the survey plan registered type.");
    }

    public static async Task AttachmentUploadReplacesExistingComputeReportSourceType()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "@id": 17,
                  "type": "uploaded_placeholder",
                  "body": { "@id": 18 },
                  "link": { "@id": 19 }
                }
                """),
            new SequencedResponse(HttpStatusCode.OK, """
                [
                  { "@id": "obj:1", "type": "st_surveyplan" },
                  { "@id": "obj:2", "type": "st_compute_report", "body": { "@id": "obj:3" } },
                  { "@id": "obj:4", "sourceType": "st_compute_report" }
                ]
                """),
            new SequencedResponse(HttpStatusCode.OK, "[]"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "jpablo",
            "secret-password",
            "token-abc",
            new InnolaUserContext("jpablo", "Juan Pablo", new[] { "Super Group" }, Array.Empty<string>()),
            null);
        var transaction = new SelectedInnolaTransaction(
            "task-1",
            "transaction-1",
            "TR100000674",
            "Compute Survey Plan",
            "Compute",
            DateTimeOffset.Parse("2026-07-22T00:00:00Z"));

        var result = await service.UploadAttachmentAsync(
            session,
            transaction,
            "compute_examination_report.pdf",
            "application/pdf",
            Encoding.UTF8.GetBytes("%PDF-1.4 test"),
            ComputeReportAttachmentService.SourceType);

        TestAssert.True(result.Success, result.ErrorMessage ?? "Compute report upload and registration should succeed.");
        var body = handler.Requests[2].Body;
        TestAssert.True(body.Contains("\"type\":\"st_surveyplan\"", StringComparison.Ordinal), "Existing non-compute sources should be preserved.");
        TestAssert.Equal(1, CountOccurrences(body, "\"type\":\"st_compute_report\""), "Only the newly uploaded compute report should remain.");
        TestAssert.True(!body.Contains("\"sourceType\":\"st_compute_report\"", StringComparison.Ordinal), "Prior compute report source variants should be removed.");
    }

    public static async Task AttachmentUploadReplacesExistingPlaOutputSourceType()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "@id": 27,
                  "type": "uploaded_placeholder",
                  "body": { "@id": 28 },
                  "link": { "@id": 29 }
                }
                """),
            new SequencedResponse(HttpStatusCode.OK, """
                [
                  { "@id": "obj:1", "type": "st_plan_annexation_pdf" },
                  { "@id": "obj:2", "type": "st_plan_annex_output", "body": { "@id": "obj:3" } },
                  { "@id": "obj:4", "sourceType": "st_plan_annex_output" },
                  { "@id": "obj:5", "type": "st_plan_annex_output2" }
                ]
                """),
            new SequencedResponse(HttpStatusCode.OK, "[]"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "jpablo",
            "secret-password",
            "token-abc",
            new InnolaUserContext("jpablo", "Juan Pablo", new[] { "Super Group" }, Array.Empty<string>()),
            null);
        var transaction = new SelectedInnolaTransaction(
            "task-1",
            "transaction-1",
            "TR100001219",
            "Plan Annexed",
            "Compute Survey Plan",
            DateTimeOffset.Parse("2026-08-24T00:00:00Z"));

        var result = await service.UploadAttachmentAsync(
            session,
            transaction,
            "pla-output-1.pdf",
            "application/pdf",
            Encoding.UTF8.GetBytes("%PDF-1.4 test"),
            "st_plan_annex_output");

        TestAssert.True(result.Success, result.ErrorMessage ?? "PLA output upload and registration should succeed.");
        var body = handler.Requests[2].Body;
        TestAssert.True(body.Contains("\"type\":\"st_plan_annexation_pdf\"", StringComparison.Ordinal), "Existing input plan source should be preserved.");
        TestAssert.True(body.Contains("\"type\":\"st_plan_annex_output2\"", StringComparison.Ordinal), "Other PLA output source slots should be preserved.");
        TestAssert.Equal(1, CountOccurrences(body, "\"type\":\"st_plan_annex_output\""), "Only the newly uploaded PLA output source should remain for this slot.");
        TestAssert.True(!body.Contains("\"sourceType\":\"st_plan_annex_output\"", StringComparison.Ordinal), "Prior PLA output source variants should be removed.");
    }

    public static async Task AttachmentUploadPreservesSafeTransportDiagnosticAndRedactsSecrets()
    {
        var session = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-test.innola-solutions.com/",
            "jpablo",
            "secret-password",
            "token-abc",
            new InnolaUserContext("jpablo", "Juan Pablo", new[] { "Super Group" }, Array.Empty<string>()),
            null);
        var transaction = new SelectedInnolaTransaction(
            "task-1",
            "transaction-1",
            "TR100000724",
            "Plan Annexed",
            "Plan Annexation",
            DateTimeOffset.Parse("2026-08-27T00:00:00Z"));
        var safeException = new HttpRequestException(
            "A connection attempt failed because the connected host did not respond. (eltrs-test.innola-solutions.com:443)",
            new TimeoutException("The operation timed out."));
        var safeHandler = new SequencedHttpMessageHandler(
            new SequencedResponse(safeException),
            new SequencedResponse(safeException),
            new SequencedResponse(safeException));
        var safeService = new InnolaTransactionDetailService(new HttpClient(safeHandler));
        var unsafeException = new HttpRequestException("token secret-password {raw response}");
        var unsafeHandler = new SequencedHttpMessageHandler(
            new SequencedResponse(unsafeException),
            new SequencedResponse(unsafeException),
            new SequencedResponse(unsafeException));
        var unsafeService = new InnolaTransactionDetailService(new HttpClient(unsafeHandler));

        var safe = await safeService.UploadAttachmentAsync(
            session,
            transaction,
            "survey_diagram_selection.png",
            "image/png",
            Encoding.UTF8.GetBytes("png"),
            "st_plan_annex_image");
        var unsafeResult = await unsafeService.UploadAttachmentAsync(
            session,
            transaction,
            "survey_diagram_selection.png",
            "image/png",
            Encoding.UTF8.GetBytes("png"),
            "st_plan_annex_image");

        TestAssert.False(safe.Success, "Transport exception should fail upload.");
        TestAssert.Equal("HttpRequestException", safe.ErrorCategory, "Transport error category mismatch.");
        TestAssert.True(safe.ErrorMessage?.Contains("eltrs-test.innola-solutions.com:443", StringComparison.Ordinal) == true, "Safe upload diagnostic should preserve host/port detail.");
        TestAssert.True(safe.ErrorMessage?.Contains("TimeoutException", StringComparison.Ordinal) == true, "Safe upload diagnostic should preserve inner exception type.");
        TestAssert.Equal("source/sources/attach", safe.Diagnostics?.Route, "Exception diagnostic should persist upload route.");
        TestAssert.Equal("bearer", safe.Diagnostics?.AuthMode, "Exception diagnostic should persist the final auth attempt.");
        TestAssert.Equal("transaction-1", safe.Diagnostics?.TaskValue, "Exception diagnostic should persist task value.");
        TestAssert.Equal("image/png", safe.Diagnostics?.ContentType, "Exception diagnostic should persist content type.");
        TestAssert.Equal(3L, safe.Diagnostics?.ByteCount, "Exception diagnostic should persist byte count.");
        TestAssert.False(unsafeResult.Success, "Unsafe diagnostic upload should fail.");
        TestAssert.True(unsafeResult.ErrorMessage?.Contains("Sensitive diagnostic was redacted", StringComparison.Ordinal) == true, "Unsafe diagnostic should be redacted.");
        TestAssert.True(unsafeResult.ErrorMessage?.Contains("secret-password", StringComparison.Ordinal) != true, "Unsafe diagnostic must not leak password text.");
        TestAssert.True(unsafeResult.ErrorMessage?.Contains("{raw response}", StringComparison.Ordinal) != true, "Unsafe diagnostic must not leak raw structured text.");
    }

    public static async Task AttachmentUploadRetriesTransportFailureWithBearerAuth()
    {
        var handler = new SequencedHttpMessageHandler(
            new SequencedResponse(new HttpRequestException("Error while copying content to a stream.")),
            new SequencedResponse(HttpStatusCode.OK, """
                {
                  "@id": 37,
                  "type": "uploaded_placeholder",
                  "body": { "@id": 38 },
                  "link": { "@id": 39 }
                }
                """),
            new SequencedResponse(HttpStatusCode.OK, "[]"),
            new SequencedResponse(HttpStatusCode.OK, "[]"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-test.innola-solutions.com/",
            "jpablo",
            "secret-password",
            "token-abc",
            new InnolaUserContext("jpablo", "Juan Pablo", new[] { "Super Group" }, Array.Empty<string>()),
            null);
        var transaction = new SelectedInnolaTransaction(
            "task-1",
            "transaction-1",
            "TR100000724",
            "Plan Annexed",
            "Plan Annexation",
            DateTimeOffset.Parse("2026-08-27T00:00:00Z"));

        var result = await service.UploadAttachmentAsync(
            session,
            transaction,
            "survey_diagram_selection.png",
            "image/png",
            Encoding.UTF8.GetBytes("png"),
            "st_plan_annex_image");

        TestAssert.True(result.Success, result.ErrorMessage ?? "Bearer retry should complete upload.");
        TestAssert.Equal(4, handler.Requests.Count, "Upload should try access token, retry with bearer, load sources, then register sources.");
        TestAssert.Equal("token-abc", handler.Requests[0].AccessToken, "First upload attempt should use access-token header.");
        TestAssert.Equal(null, handler.Requests[0].AuthorizationScheme, "First upload attempt should not use bearer authorization.");
        TestAssert.Equal(null, handler.Requests[1].AccessToken, "Bearer retry should not keep access-token header.");
        TestAssert.Equal("Bearer", handler.Requests[1].AuthorizationScheme, "Second upload attempt should use bearer authorization.");
        TestAssert.Equal("bearer", result.Diagnostics?.AuthMode, "Diagnostics should persist the successful auth mode.");
        TestAssert.Equal("source/sources/attach", result.Diagnostics?.Route, "Diagnostics should persist upload route.");
        TestAssert.Equal("query_only", result.Diagnostics?.BindingMode, "Diagnostics should persist binding mode.");
        TestAssert.Equal("attach_then_register_source", result.Diagnostics?.UploadMode, "Diagnostics should persist upload mode.");
        TestAssert.Equal("transaction-1", result.Diagnostics?.TaskValue, "Diagnostics should persist task value used for upload.");
        TestAssert.Equal("image/png", result.Diagnostics?.ContentType, "Diagnostics should persist content type.");
        TestAssert.Equal(3L, result.Diagnostics?.ByteCount, "Diagnostics should persist byte count.");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
            var accessToken = request.Headers.TryGetValues("Access-Token", out var values)
                ? values.FirstOrDefault()
                : null;
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body, accessToken, request.Headers.Authorization?.Scheme));

            var response = responses.Dequeue();
            if (response.Exception is not null)
            {
                throw response.Exception;
            }

            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body, string? AccessToken, string? AuthorizationScheme);

    private sealed record SequencedResponse(HttpStatusCode StatusCode, string Body)
    {
        public SequencedResponse(Exception exception)
            : this(HttpStatusCode.OK, string.Empty)
        {
            Exception = exception;
        }

        public Exception? Exception { get; }
    }
}
