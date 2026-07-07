using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaPlanCheckServiceTests
{
    public static async Task WritesPlanChecklistAndPreservesPlanPayload()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(new[]
        {
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "uid": "uid-1",
                "unknownField": "preserve-me",
                "link": { "id": "link-1" },
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null },
                  { "@c": "PlanCheck", "id": "area", "checkType": "plan_check_type_area", "passed": false, "description": "old area" },
                  { "@c": "PlanCheck", "id": "plotting", "checkType": "plan_check_type_plotting", "passed": null, "description": null },
                  { "@c": "PlanCheck", "id": "notices", "checkType": "plan_check_type_notices", "passed": null, "description": null },
                  { "@c": "PlanCheck", "id": "adjoining", "checkType": "plan_check_type_adjoining", "passed": null, "description": null }
                ]
              }
            ]
            """,
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "checkList": []
              }
            ]
            """
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan Check writeback should succeed.");
        TestAssert.Equal(2, handler.Requests.Count, "Plan Check service should GET then POST.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "GET route mismatch.");
        TestAssert.True(handler.Requests[0].PathAndQuery.Contains("transactionId=100000004", StringComparison.OrdinalIgnoreCase), "GET transaction binding missing.");
        TestAssert.Equal("token-abc", handler.AccessTokens[0], "GET should use the active Innola access token.");
        TestAssert.Equal("token-abc", handler.AccessTokens[1], "POST should use the active Innola access token.");

        using var posted = JsonDocument.Parse(handler.Bodies[1]);
        var plan = posted.RootElement[0];
        TestAssert.Equal("preserve-me", plan.GetProperty("unknownField").GetString(), "Unknown Plan fields must be preserved.");
        TestAssert.Equal("link-1", plan.GetProperty("link").GetProperty("id").GetString(), "Nested API-generated fields must be preserved.");
        var checkList = plan.GetProperty("checkList");
        TestAssert.True(checkList.EnumerateArray().Any(item =>
            item.GetProperty("checkType").GetString() == "plan_check_type_closure"
            && item.GetProperty("passed").GetBoolean()), "Closure should be accepted from report evidence.");
        TestAssert.True(checkList.EnumerateArray().Any(item =>
            item.GetProperty("checkType").GetString() == "plan_check_type_area"
            && item.GetProperty("passed").GetBoolean()
            && item.GetProperty("description").GetString()!.Contains("2 generated polygon", StringComparison.OrdinalIgnoreCase)), "Area should be accepted from output summary evidence.");
        var notices = checkList.EnumerateArray().First(item => item.GetProperty("checkType").GetString() == "plan_check_type_notices");
        TestAssert.Equal(JsonValueKind.Null, notices.GetProperty("passed").ValueKind, "Notices should remain N/A when no automated rule exists.");
        var adjoining = checkList.EnumerateArray().First(item => item.GetProperty("checkType").GetString() == "plan_check_type_adjoining");
        TestAssert.Equal(JsonValueKind.Null, adjoining.GetProperty("passed").ValueKind, "Adjoining should remain N/A when no automated comparator rule exists.");

        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "plan_check_api_request.json")), "Plan Check request evidence should be written.");
        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "plan_check_api_response.json")), "Plan Check response evidence should be written.");
        var requestEvidence = File.ReadAllText(Path.Combine(layout.WorkingDirectory, "plan_check_api_request.json"));
        TestAssert.True(!requestEvidence.Contains("token-abc", StringComparison.OrdinalIgnoreCase), "Request evidence must not log access tokens.");
        TestAssert.True(requestEvidence.Contains("plan_check_type_closure", StringComparison.OrdinalIgnoreCase), "Request evidence should list updated check types.");
        TestAssert.True(requestEvidence.Contains("preserved_unsupported_check_types", StringComparison.OrdinalIgnoreCase), "Request evidence should show unsupported preserved Plan Check rows.");
        TestAssert.True(requestEvidence.Contains("plan_check_type_adjoining", StringComparison.OrdinalIgnoreCase), "Request evidence should show adjoining/comparator automation limitation.");
    }

    public static async Task DoesNotPassRowsForPendingStageStatus()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout, dimensionStatus: "pending");
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(new[]
        {
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null },
                  { "@c": "PlanCheck", "id": "details", "checkType": "plan_check_type_details", "passed": null, "description": null }
                ]
              }
            ]
            """,
            "[{\"@c\":\"Plan\",\"id\":\"plan-1\",\"checkList\":[]}]"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan Check writeback should still save explicit failed checklist values.");
        using var posted = JsonDocument.Parse(handler.Bodies[1]);
        var checkList = posted.RootElement[0].GetProperty("checkList");
        TestAssert.True(checkList.EnumerateArray().Any(item =>
            item.GetProperty("checkType").GetString() == "plan_check_type_closure"
            && item.GetProperty("passed").ValueKind == JsonValueKind.False), "Closure must not pass when Dimension Check is pending.");
        TestAssert.True(checkList.EnumerateArray().Any(item =>
            item.GetProperty("checkType").GetString() == "plan_check_type_details"
            && item.GetProperty("passed").ValueKind == JsonValueKind.False), "Details must not pass when Dimension Check is pending.");
    }

    public static async Task FailsWhenChecklistIsMissing()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(new[] { "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]" });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(!result.Success, "Missing checklist should fail.");
        TestAssert.Equal("checklist_missing", result.ErrorCategory, "Missing checklist category mismatch.");
        TestAssert.Equal(1, handler.Requests.Count, "Missing checklist must not POST.");
        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "plan_check_api_failure.json")), "Failure evidence should be written.");
    }

    public static async Task FailsBeforeHttpWhenUnauthorized()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        var handler = new RecordingHandler(Array.Empty<string>());
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session() with { AccessToken = string.Empty }, Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(!result.Success, "Unauthorized session should fail.");
        TestAssert.Equal("unauthorized", result.ErrorCategory, "Unauthorized category mismatch.");
        TestAssert.Equal(0, handler.Requests.Count, "Unauthorized Plan Check service must not issue HTTP requests.");
    }

    private static InnolaSession Session()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-abc",
            new InnolaUserContext("tester", "Test User", Array.Empty<string>(), Array.Empty<string>()),
            null);
    }

    private static SelectedInnolaTransaction Transaction()
    {
        return new SelectedInnolaTransaction(
            "task-100000004",
            "100000004",
            "TR100000004",
            "Computation Check",
            "parcel_workflow",
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            null,
            "Plan Examination");
    }

    private static CaseFolderLayout CreateLayout(string root)
    {
        var layout = CaseFolderLayout.For(root, "TR100000004");
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        Directory.CreateDirectory(layout.ReportsDirectory);
        return layout;
    }

    private static ComputeReviewDispositionDocument Disposition(CaseFolderLayout layout)
    {
        return new ComputeReviewDispositionDocument(
            "compute_review_disposition_v1",
            "100000004",
            "TR100000004",
            "task-100000004",
            "approved",
            "Approved for closeout.",
            "tester",
            "2026-06-10T12:00:00.0000000Z",
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"),
            "run-output",
            "written",
            Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json"),
            "saved",
            "su-100000004",
            "TR100000004-completed.zip",
            "sidwell_completed_package",
            "pending",
            Path.Combine(layout.ReportsDirectory, "compute_examination_report.json"));
    }

    private static void WriteReport(CaseFolderLayout layout, string dimensionStatus = "passed")
    {
        File.WriteAllText(
            Path.Combine(layout.ReportsDirectory, "compute_examination_report.json"),
            """
            {
              "schema_version": "compute_examination_report_v1",
              "transaction_id": "100000004",
              "transaction_number": "TR100000004",
              "stages": [
                { "stage_id": "structure_check", "stage_name": "Structure Check", "status": "passed", "findings": [
                  { "rule_id": "primary_computation_sheet", "display_name": "Primary computation sheet", "outcome": "passed", "severity": "info", "message": "Computation sheet is primary." }
                ] },
                { "stage_id": "dimension_check", "stage_name": "Dimension Check", "status": "__DIMENSION_STATUS__", "findings": [] },
                { "stage_id": "validate_points_and_lines", "stage_name": "Validate Points and Lines", "status": "approved", "findings": [] },
                { "stage_id": "create_spatial_units", "stage_name": "Create Spatial Units", "status": "created", "findings": [] },
                { "stage_id": "final_review", "stage_name": "Final Review", "status": "approved", "findings": [] },
                { "stage_id": "enterprise_working_publish", "stage_name": "Enterprise working-layer publish", "status": "succeeded", "findings": [] },
                { "stage_id": "enterprise_disposition", "stage_name": "Enterprise disposition writeback", "status": "written", "findings": [] }
              ],
              "closeout": {
                "decision": "approved"
              }
            }
            """.Replace("__DIMENSION_STATUS__", dimensionStatus, StringComparison.Ordinal));
    }

    private static void WriteOutputSummary(CaseFolderLayout layout)
    {
        File.WriteAllText(
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            """
            {
              "payload": {
                "polygon_count": 2
              }
            }
            """);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<string> responses;

        public RecordingHandler(IEnumerable<string> responses)
        {
            this.responses = new Queue<string>(responses);
        }

        public List<Uri> Requests { get; } = new();

        public List<string> Bodies { get; } = new();

        public List<string?> AccessTokens { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            AccessTokens.Add(request.Headers.TryGetValues("Access-Token", out var values) ? values.FirstOrDefault() : null);
            var response = responses.Count > 0 ? responses.Dequeue() : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
