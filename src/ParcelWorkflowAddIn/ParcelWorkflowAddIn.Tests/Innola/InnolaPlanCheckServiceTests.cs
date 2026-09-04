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
        TestAssert.Equal(2, handler.Requests.Count, "Plan Check service should GET then PUT.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("/api/v4/rest/data/objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "GET route mismatch.");
        TestAssert.Equal(HttpMethod.Get, handler.Methods[0], "Plan fetch should use GET.");
        TestAssert.Equal(HttpMethod.Put, handler.Methods[1], "Plan save should use PUT.");
        TestAssert.True(handler.Requests[0].PathAndQuery.Contains("transactionId=100000004", StringComparison.OrdinalIgnoreCase), "GET transaction binding missing.");
        TestAssert.Equal("token-abc", handler.AccessTokens[0], "GET should use the active Innola access token.");
        TestAssert.Equal("token-abc", handler.AccessTokens[1], "PUT should use the active Innola access token.");

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


    public static async Task WritesReviewedNeighborsIntoPlanPayload()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        WriteReviewArtifact(layout);
        var handler = new RecordingHandler(new[]
        {
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "unknownField": "preserve-me",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                ],
                "neighbors": [
                  {
                    "@c": "Neighbor",
                    "id": "existing-neighbor",
                    "unknownNeighborField": "keep-me",
                    "neighborType": "neighbor_type_owner",
                    "name": "Blossom Bennett",
                    "address": "Old Address",
                    "volume": "1158",
                    "folio": "604",
                    "lot": "7",
                    "landValNumber": "LV-7",
                    "examNumber": "EX-7"
                  }
                ]
              }
            ]
            """,
            """
            {
              "@c": "Neighbor",
              "@id": "obj:1",
              "versionRev": 0,
              "id": "01a0692d-b209-7975-9eca-5c903b962f77",
              "neighborType": "neighbor_type_owner",
              "name": null,
              "address": null,
              "volume": null,
              "folio": null,
              "lot": null,
              "landValNumber": null,
              "examNumber": null,
              "allowRead": true,
              "allowWrite": true
            }
            """,
            "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan Examination writeback with neighbors should succeed.");
        TestAssert.Equal(3, handler.Requests.Count, "Service should fetch Plan, create one Neighbor template, then save Plan.");
        TestAssert.Equal(HttpMethod.Get, handler.Methods[0], "Plan fetch should use GET.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("/api/v4/rest/data/objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Plan fetch route mismatch.");
        TestAssert.Equal(HttpMethod.Post, handler.Methods[1], "Neighbor template creation should use POST.");
        TestAssert.True(handler.Requests[1].PathAndQuery!.Contains("/api/v4/rest/data/objects/create", StringComparison.OrdinalIgnoreCase), "Neighbor create route mismatch.");
        TestAssert.Equal(HttpMethod.Put, handler.Methods[2], "Plan save should use PUT.");
        TestAssert.True(handler.Requests[2].PathAndQuery!.Contains("/api/v4/rest/data/objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Plan save route mismatch.");
        using (var createBody = JsonDocument.Parse(handler.Bodies[1]))
        {
            TestAssert.Equal("Neighbor", createBody.RootElement.GetProperty("@c").GetString(), "Create-template body should request a Neighbor object.");
        }

        using var posted = JsonDocument.Parse(handler.Bodies[2]);
        var plan = posted.RootElement[0];
        TestAssert.Equal("preserve-me", plan.GetProperty("unknownField").GetString(), "Unknown Plan fields must be preserved.");
        var neighbors = plan.GetProperty("neighbors").EnumerateArray().ToArray();
        TestAssert.Equal(2, neighbors.Length, "Reviewed neighbors should update existing rows and add new rows without duplicating representatives or blanks.");
        var existing = neighbors.First(item => item.GetProperty("id").GetString() == "existing-neighbor");
        TestAssert.Equal("keep-me", existing.GetProperty("unknownNeighborField").GetString(), "Unknown Neighbor fields must be preserved.");
        TestAssert.Equal("Updated Blossom Address", existing.GetProperty("address").GetString(), "Duplicate reviewed Neighbor should refresh editable fields.");
        var added = neighbors.First(item => item.GetProperty("name").GetString() == "Adrian Duncanson");
        TestAssert.Equal("neighbor_type_owner", added.GetProperty("neighborType").GetString(), "Neighbor type should default to owner until Innola confirms a more precise value.");
        TestAssert.Equal("Adrian Road", added.GetProperty("address").GetString(), "Neighbor address mapping mismatch.");
        TestAssert.Equal("1158", added.GetProperty("volume").GetString(), "Neighbor volume mapping mismatch.");
        TestAssert.Equal("604", added.GetProperty("folio").GetString(), "Neighbor folio mapping mismatch.");
        TestAssert.Equal("12", added.GetProperty("lot").GetString(), "Neighbor lot mapping mismatch.");
        TestAssert.Equal("LV-12", added.GetProperty("landValNumber").GetString(), "Neighbor land valuation mapping mismatch.");
        TestAssert.Equal("EX-12", added.GetProperty("examNumber").GetString(), "Neighbor examination number mapping mismatch.");

        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "plan_examination_api_request.json")), "Plan Examination request evidence should be written.");
        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "plan_examination_api_response.json")), "Plan Examination response evidence should be written.");
        var requestEvidence = File.ReadAllText(Path.Combine(layout.WorkingDirectory, "plan_examination_api_request.json"));
        TestAssert.True(!requestEvidence.Contains("token-abc", StringComparison.OrdinalIgnoreCase), "Plan Examination evidence must not log access tokens.");
        TestAssert.True(requestEvidence.Contains("neighbor_rows_created", StringComparison.OrdinalIgnoreCase), "Plan Examination evidence should include Neighbor writeback counts.");
        TestAssert.True(requestEvidence.Contains("Adrian Duncanson", StringComparison.OrdinalIgnoreCase), "Plan Examination evidence should identify reviewed Neighbor rows.");
    }



    public static async Task FallsBackToLocalNeighborWhenTemplateCreateReturnsBadRequest()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        WriteReviewArtifactRaw(layout,
            """
            {
              "schema_version": "extraction_review_data_v1",
              "transaction_number": "TR100000004",
              "adjacent_owners": [
                {
                  "name": "Adrian Duncanson",
                  "role": "Neighbor",
                  "address": "Adrian Road",
                  "volume": "1158",
                  "folio": "604",
                  "lot_number": "12",
                  "land_valuation_number": "LV-12",
                  "examination_number": "EX-12"
                }
              ]
            }
            """);
        var handler = new RecordingHandler(
            new[]
            {
                """
                [
                  {
                    "@c": "Plan",
                    "id": "plan-1",
                    "checkList": [
                      { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                    ]
                  }
                ]
                """,
                "{}",
                "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]"
            },
            new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.OK });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, $"Neighbor writeback should fall back to a local nested Neighbor when create-template returns BadRequest. Message={result.Message}; Category={result.ErrorCategory}");
        TestAssert.Equal(3, handler.Requests.Count, "Service should fetch Plan, attempt Neighbor template, then save Plan with local Neighbor fallback.");
        TestAssert.Equal(HttpMethod.Post, handler.Methods[1], "Neighbor template endpoint should still be attempted first.");
        TestAssert.Equal(HttpMethod.Put, handler.Methods[2], "Plan save should still run after local Neighbor fallback.");
        using var posted = JsonDocument.Parse(handler.Bodies[2]);
        var added = posted.RootElement[0].GetProperty("neighbors").EnumerateArray().First(item => item.GetProperty("name").GetString() == "Adrian Duncanson");
        TestAssert.Equal("Neighbor", added.GetProperty("@c").GetString(), "Local fallback Neighbor should preserve the Innola class name.");
        TestAssert.Equal("neighbor_type_owner", added.GetProperty("neighborType").GetString(), "Local fallback Neighbor should include the default neighbor type.");
        TestAssert.Equal("Adrian Road", added.GetProperty("address").GetString(), "Reviewed Neighbor fields should still populate after local fallback.");
        TestAssert.True(!File.Exists(Path.Combine(layout.WorkingDirectory, "plan_examination_api_failure.json")), "Successful local fallback should not leave a Plan Examination failure artifact.");
    }
    public static async Task FallsBackToTransactionNumberWhenUuidPlanLookupIsEmpty()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(new[]
        {
            "[]",
            "[]",
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                ]
              }
            ]
            """,
            "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));
        var transaction = TransactionWithIds("019fe6d0-0c82-7009-b92e-10248f82a968", "100000622");

        var result = await service.WriteAsync(Session(), transaction, layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan lookup should fall back to transaction number when UUID lookup returns no Plan objects.");
        TestAssert.Equal(4, handler.Requests.Count, "Plan lookup should try UUID data, UUID administrative, transaction-number data, then save.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("transactionId=019fe6d0-0c82-7009-b92e-10248f82a968", StringComparison.OrdinalIgnoreCase), "First Plan lookup should use selected transaction id.");
        TestAssert.True(handler.Requests[1].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Second Plan lookup should try the administrative route with selected transaction id.");
        TestAssert.True(handler.Requests[1].PathAndQuery!.Contains("transactionId=019fe6d0-0c82-7009-b92e-10248f82a968", StringComparison.OrdinalIgnoreCase), "Administrative UUID Plan lookup should use selected transaction id.");
        TestAssert.True(handler.Requests[2].PathAndQuery!.Contains("transactionId=100000622", StringComparison.OrdinalIgnoreCase), "Fallback Plan lookup should use displayed transaction number.");
        TestAssert.True(handler.Requests[3].PathAndQuery!.Contains("transactionId=100000622", StringComparison.OrdinalIgnoreCase), "Plan save should use the lookup id that returned the Plan.");
    }

    public static async Task FallsBackToTransactionNumberWhenUuidPlanLookupReturnsServerError()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(
            new[]
            {
                "{}",
                "[]",
                """
                [
                  {
                    "@c": "Plan",
                    "id": "plan-1",
                    "checkList": [
                      { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                    ]
                  }
                ]
                """,
                "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]"
            },
            new[] { HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK });
        var service = new InnolaPlanCheckService(new HttpClient(handler));
        var transaction = TransactionWithIds("019fe6d0-0c82-7009-b92e-10248f82a968", "100000622");

        var result = await service.WriteAsync(Session(), transaction, layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan lookup should fall back to transaction number when UUID lookup returns a server error.");
        TestAssert.Equal(4, handler.Requests.Count, "Plan lookup should try UUID data, UUID administrative, transaction-number data, then save.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("transactionId=019fe6d0-0c82-7009-b92e-10248f82a968", StringComparison.OrdinalIgnoreCase), "First Plan lookup should use selected transaction id.");
        TestAssert.True(handler.Requests[1].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Second Plan lookup should try the administrative route after UUID data server error.");
        TestAssert.True(handler.Requests[2].PathAndQuery!.Contains("transactionId=100000622", StringComparison.OrdinalIgnoreCase), "Fallback Plan lookup should use displayed transaction number after UUID route attempts.");
        TestAssert.True(handler.Requests[3].PathAndQuery!.Contains("transactionId=100000622", StringComparison.OrdinalIgnoreCase), "Plan save should use the lookup id that returned the Plan.");
    }
    public static async Task FallsBackToAdministrativePlanRouteWhenDataPlanRouteFails()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(
            new[]
            {
                "[]",
                "[]",
                "{}",
                "{}",
                "{}",
                """
                [
                  {
                    "@c": "Plan",
                    "id": "plan-1",
                    "checkList": [
                      { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                    ]
                  }
                ]
                """,
                "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]"
            },
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.OK,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.OK,
                HttpStatusCode.OK
            });
        var service = new InnolaPlanCheckService(new HttpClient(handler));
        var transaction = TransactionWithIds("019fe6d2-7c02-7bf5-95f3-08d2e47635b0", "100000623");

        var result = await service.WriteAsync(Session(), transaction, layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan lookup should fall back to administrative route when data route fails for transaction number.");
        TestAssert.Equal(7, handler.Requests.Count, "Plan lookup should try UUID data/admin, retry data transaction number, fetch administrative transaction number, then save.");
        TestAssert.True(handler.Requests[0].PathAndQuery!.Contains("/api/v4/rest/data/objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Primary route should use data objects.");
        TestAssert.True(handler.Requests[1].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Second route should try administrative objects with the UUID.");
        TestAssert.True(handler.Requests[2].PathAndQuery!.Contains("/api/v4/rest/data/objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Fallback data route should use data objects with the transaction number.");
        TestAssert.True(handler.Requests[5].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Fallback route should use administrative Plan objects.");
        TestAssert.True(handler.Requests[6].PathAndQuery!.Contains("/api/v4/rest/administrative/ladm-objects?typeKeyId=plan", StringComparison.OrdinalIgnoreCase), "Save should use the route that returned the Plan.");
        TestAssert.Equal(HttpMethod.Post, handler.Methods[6], "Administrative Plan save should use POST.");
    }
    public static async Task FailureEvidenceNamesBothLookupKeysWhenUuidAndTransactionNumberPlanLookupsFail()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        var handler = new RecordingHandler(
            Enumerable.Repeat("{}", 8),
            Enumerable.Repeat(HttpStatusCode.InternalServerError, 8));
        var service = new InnolaPlanCheckService(new HttpClient(handler));
        var transaction = TransactionWithIds("019fe6d0-0c82-7009-b92e-10248f82a968", "100000622");

        var result = await service.WriteAsync(Session(), transaction, layout.RootDirectory, Disposition(layout));

        TestAssert.True(!result.Success, "Plan lookup should fail when both UUID and transaction-number lookups fail.");
        TestAssert.Equal(nameof(HttpRequestException), result.ErrorCategory, "Failure category should preserve the HTTP exception type.");
        TestAssert.Equal(8, handler.Requests.Count, "Primary UUID route attempts should fail fast once each, then data and administrative transaction-number fallbacks should use normal retry attempts.");
        var failureEvidence = File.ReadAllText(Path.Combine(layout.WorkingDirectory, "plan_examination_api_failure.json"));
        TestAssert.True(failureEvidence.Contains("failed for all lookup keys and Plan routes", StringComparison.OrdinalIgnoreCase), "Failure evidence should explain that both lookup keys and routes failed.");
        TestAssert.True(failureEvidence.Contains("019fe6d0-0c82-7009-b92e-10248f82a968", StringComparison.OrdinalIgnoreCase), "Failure evidence should include the UUID lookup key.");
        TestAssert.True(failureEvidence.Contains("100000622", StringComparison.OrdinalIgnoreCase), "Failure evidence should include the transaction-number lookup key.");
    }

    public static async Task PreservesSinglePlanShapeAndAcceptsDataArrayNeighborTemplate()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        WriteReviewArtifactRaw(layout,
            """
            {
              "schema_version": "extraction_review_data_v1",
              "transaction_number": "TR100000004",
              "adjacent_owners": [
                {
                  "name": "Faith Smith",
                  "role": "Neighbor",
                  "address": "Faith Road",
                  "volume": "1200",
                  "folio": "700"
                }
              ]
            }
            """);
        var handler = new RecordingHandler(new[]
        {
            """
            {
              "@c": "Plan",
              "id": "plan-1",
              "checkList": [
                { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
              ]
            }
            """,
            """
            {
              "data": [
                {
                  "@c": "Neighbor",
                  "id": "template-neighbor",
                  "neighborType": "neighbor_type_owner",
                  "allowRead": true,
                  "allowWrite": true
                }
              ]
            }
            """,
            "{\"@c\":\"Plan\",\"id\":\"plan-1\"}"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Single Plan writeback with data-array Neighbor template should succeed.");
        using var posted = JsonDocument.Parse(handler.Bodies[2]);
        TestAssert.Equal(JsonValueKind.Object, posted.RootElement.ValueKind, "Plan save should preserve a single-object GET response shape.");
        var added = posted.RootElement.GetProperty("neighbors").EnumerateArray().Single();
        TestAssert.Equal("Neighbor", added.GetProperty("@c").GetString(), "data array envelope should resolve to the Neighbor object.");
        TestAssert.Equal("Faith Smith", added.GetProperty("name").GetString(), "Neighbor name should be populated from reviewed data.");
    }

    public static async Task TrimsNeighborRolesAndPreservesExistingValuesForPartialRows()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        WriteReviewArtifactRaw(layout,
            """
            {
              "schema_version": "extraction_review_data_v1",
              "transaction_number": "TR100000004",
              "adjacent_owners": [
                {
                  "name": "Enid Williams",
                  "role": " Neighbor ",
                  "address": "",
                  "volume": "",
                  "folio": ""
                }
              ]
            }
            """);
        var handler = new RecordingHandler(new[]
        {
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                ],
                "neighbors": [
                  {
                    "@c": "Neighbor",
                    "id": "existing-neighbor",
                    "neighborType": "neighbor_type_owner",
                    "name": "Enid Williams",
                    "address": "Existing Address",
                    "volume": "1300",
                    "folio": "800",
                    "lot": "9",
                    "landValNumber": "LV-9",
                    "examNumber": "EX-9"
                  }
                ]
              }
            ]
            """,
            "[{\"@c\":\"Plan\",\"id\":\"plan-1\"}]"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Trimmed Neighbor role should be accepted.");
        TestAssert.Equal(2, handler.Requests.Count, "Existing partial Neighbor row should update without create-template call.");
        using var posted = JsonDocument.Parse(handler.Bodies[1]);
        var neighbor = posted.RootElement[0].GetProperty("neighbors").EnumerateArray().Single();
        TestAssert.Equal("Existing Address", neighbor.GetProperty("address").GetString(), "Blank reviewed address must not erase existing Neighbor address.");
        TestAssert.Equal("1300", neighbor.GetProperty("volume").GetString(), "Blank reviewed volume must not erase existing Neighbor volume.");
        TestAssert.Equal("EX-9", neighbor.GetProperty("examNumber").GetString(), "Blank reviewed exam number must not erase existing Neighbor exam number.");
    }

    public static async Task FailsWhenNeighborTemplateResponseIsMalformed()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        WriteReviewArtifact(layout);
        var handler = new RecordingHandler(new[]
        {
            """
            [
              {
                "@c": "Plan",
                "id": "plan-1",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                ]
              }
            ]
            """,
            "{\"data\":{\"@c\":\"Plan\",\"id\":\"not-a-neighbor\"}}"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(!result.Success, "Malformed Neighbor create-template response should fail writeback.");
        TestAssert.Equal(nameof(InvalidOperationException), result.ErrorCategory, "Malformed Neighbor template should be categorized as a contract failure.");
        TestAssert.Equal(2, handler.Requests.Count, "Malformed Neighbor template must stop before Plan save.");
        TestAssert.True(File.Exists(Path.Combine(layout.WorkingDirectory, "plan_examination_api_failure.json")), "Plan Examination failure evidence should be written.");
        var failureEvidence = File.ReadAllText(Path.Combine(layout.WorkingDirectory, "plan_examination_api_failure.json"));
        TestAssert.True(failureEvidence.Contains("Plan Examination Neighbor", StringComparison.OrdinalIgnoreCase), "Failure evidence should identify Neighbor writeback as the blocker.");
    }

    public static async Task ChoosesSinglePlanWithExistingNeighborsWhenMultiplePlansReturned()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        WriteReviewArtifactRaw(layout,
            """
            {
              "schema_version": "extraction_review_data_v1",
              "transaction_number": "TR100000004",
              "adjacent_owners": [
                {
                  "name": "Robert Duncanson",
                  "role": "Neighbor",
                  "address": "Robert Road",
                  "volume": "1500",
                  "folio": "900"
                }
              ]
            }
            """);
        var handler = new RecordingHandler(new[]
        {
            """
            [
              {
                "@c": "Plan",
                "id": "plan-without-neighbors",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure-a", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                ]
              },
              {
                "@c": "Plan",
                "id": "plan-with-neighbors",
                "checkList": [
                  { "@c": "PlanCheck", "id": "closure-b", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                ],
                "neighbors": []
              }
            ]
            """,
            """
            {
              "@c": "Neighbor",
              "id": "template-neighbor",
              "neighborType": "neighbor_type_owner"
            }
            """,
            "[{\"@c\":\"Plan\",\"id\":\"plan-with-neighbors\"}]"
        });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Multiple Plan response should use the one existing Neighbor collection.");
        using var posted = JsonDocument.Parse(handler.Bodies[2]);
        var plans = posted.RootElement.EnumerateArray().ToArray();
        TestAssert.True(!plans[0].TryGetProperty("neighbors", out _), "Neighbor collection should not be added to the wrong Plan.");
        var selectedNeighbors = plans[1].GetProperty("neighbors").EnumerateArray().ToArray();
        TestAssert.Equal(1, selectedNeighbors.Length, "Selected Plan should receive reviewed Neighbor row.");
        TestAssert.Equal("Robert Duncanson", selectedNeighbors[0].GetProperty("name").GetString(), "Neighbor should be attached to Plan that already owns neighbors[].");
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
        TestAssert.Equal(1, handler.Requests.Count, "Missing checklist must not PUT.");
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

    public static async Task RetriesCookieOnlyWhenAccessTokenRejected()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WriteReport(layout);
        WriteOutputSummary(layout);
        InnolaHttpClientFactory.EnsureCookie("https://eltrs-dev.innola-solutions.com/", "INNOLAID", "cookie-value");
        var handler = new RecordingHandler(
            new[]
            {
                "{}",
                "[]",
                """
                [
                  {
                    "@c": "Plan",
                    "id": "plan-1",
                    "checkList": [
                      { "@c": "PlanCheck", "id": "closure", "checkType": "plan_check_type_closure", "passed": null, "description": null }
                    ]
                  }
                ]
                """,
                "[{\"@c\":\"Plan\",\"id\":\"plan-1\",\"checkList\":[]}]"
            },
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK });
        var service = new InnolaPlanCheckService(new HttpClient(handler));

        var result = await service.WriteAsync(Session(), Transaction(), layout.RootDirectory, Disposition(layout));

        TestAssert.True(result.Success, "Plan Check writeback should retry with cookie-only auth after token Unauthorized.");
        TestAssert.Equal(4, handler.Requests.Count, "Plan Check service should retry GET once, try the administrative UUID fallback, then save.");
        TestAssert.Equal("token-abc", handler.AccessTokens[0], "First GET should send Access-Token.");
        TestAssert.True(handler.AccessTokens[1] is null, "Cookie-only GET retry should omit Access-Token.");
        TestAssert.Equal("token-abc", handler.AccessTokens[2], "Administrative UUID fallback should return to Access-Token auth.");
        TestAssert.Equal("token-abc", handler.AccessTokens[3], "Save should still use Access-Token when GET retry succeeds.");
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


    private static SelectedInnolaTransaction TransactionWithIds(string transactionId, string transactionNumber)
    {
        return new SelectedInnolaTransaction(
            "task-100000004",
            transactionId,
            transactionNumber,
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


    private static void WriteReviewArtifact(CaseFolderLayout layout)
    {
        File.WriteAllText(
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            """
            {
              "schema_version": "extraction_review_data_v1",
              "transaction_number": "TR100000004",
              "adjacent_owners": [
                {
                  "name": "Adrian Duncanson",
                  "role": "Neighbor",
                  "address": "Adrian Road",
                  "volume": "1158",
                  "folio": "604",
                  "lot_number": "12",
                  "land_valuation_number": "LV-12",
                  "examination_number": "EX-12"
                },
                {
                  "name": "Blossom Bennett",
                  "role": "neighbor",
                  "address": "Updated Blossom Address",
                  "volume": "1158",
                  "folio": "604",
                  "lot_number": "7",
                  "land_valuation_number": "LV-7",
                  "examination_number": "EX-7"
                },
                {
                  "name": "K. Dyer",
                  "role": "Representative",
                  "address": "Representative Road",
                  "volume": "999",
                  "folio": "111"
                },
                {
                  "name": "",
                  "role": "Neighbor"
                }
              ]
            }
            """);
    }


    private static void WriteReviewArtifactRaw(CaseFolderLayout layout, string json)
    {
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"), json);
    }
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<string> responses;
        private readonly Queue<HttpStatusCode> statusCodes;

        public RecordingHandler(IEnumerable<string> responses)
            : this(responses, Array.Empty<HttpStatusCode>())
        {
        }

        public RecordingHandler(IEnumerable<string> responses, IEnumerable<HttpStatusCode> statusCodes)
        {
            this.responses = new Queue<string>(responses);
            this.statusCodes = new Queue<HttpStatusCode>(statusCodes);
        }

        public List<Uri> Requests { get; } = new();

        public List<HttpMethod> Methods { get; } = new();

        public List<string> Bodies { get; } = new();

        public List<string?> AccessTokens { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            Methods.Add(request.Method);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            AccessTokens.Add(request.Headers.TryGetValues("Access-Token", out var values) ? values.FirstOrDefault() : null);
            var response = responses.Count > 0 ? responses.Dequeue() : "{}";
            var statusCode = statusCodes.Count > 0 ? statusCodes.Dequeue() : HttpStatusCode.OK;
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
