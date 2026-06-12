using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using System.Net;
using System.Text;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionDetailServiceTests
{
    public static async Task LiveDetailMapsTaskMetadataAndDownloadsSource()
    {
        var handler = new SequenceHandler(
            new Response("""
                {
                  "id": "task-1",
                  "name": "Computation Check",
                  "assignee": "tester",
                  "role": "ROLE_Survey",
                  "transactionId": "tx-1",
                  "transactionCode": "DM",
                  "transaction": {
                    "id": "tx-1",
                    "transactionNo": "TR100000004",
                    "transactionType": "DM"
                  },
                  "application": {
                    "sources": [
                      {
                        "id": "source-1",
                        "fileName": "plan_map.pdf",
                        "mimeType": "application/pdf",
                        "category": "plan",
                        "size": 4
                      }
                    ]
                  }
                }
                """, "application/json"),
            new Response("PDF!", "application/pdf"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = Session();
        var selected = new SelectedInnolaTransaction("task-1", "tx-1", "TR100000004", "Computation Check", "parcel_workflow", DateTimeOffset.UtcNow);

        var detail = await service.GetTransactionDetailAsync(session, selected);
        var content = await service.GetAttachmentContentAsync(session, detail.Detail!, detail.Detail!.Attachments[0]);

        TestAssert.True(detail.Success, "Detail should load.");
        TestAssert.Equal("TR100000004", detail.Detail?.TransactionNumber, "Transaction number mismatch.");
        TestAssert.Equal("DM", detail.Detail?.CaseType, "Case type mismatch.");
        TestAssert.Equal(1, detail.Detail?.Attachments.Count ?? -1, "Attachment count mismatch.");
        TestAssert.Equal(SourceRole.PlanMapReference, detail.Detail!.Attachments[0].SourceRole, "Source role mismatch.");
        TestAssert.True(content.Success, "Attachment content should download.");
        TestAssert.Equal(2, handler.Requests.Count, "Detail and download endpoints should be called.");
        TestAssert.True(handler.Requests[0].Uri.AbsoluteUri.EndsWith("/api/v4/rest/workflow/tasks/task-1", StringComparison.Ordinal), "Detail endpoint mismatch.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.Contains("/api/rest/scanning/source/source-1/body", StringComparison.Ordinal), "Scanning source body endpoint mismatch.");
    }

    public static async Task LiveDetailFallsBackToTransactionSourcesAndDownloadsBody()
    {
        var handler = new SequenceHandler(
            new Response("""
                {
                  "id": "task-1",
                  "name": "Assign Computation Task",
                  "transactionId": "tx-1",
                  "transactionCode": "PE",
                  "transaction": {
                    "id": "tx-1",
                    "transactionNo": "TR100000206",
                    "transactionType": "Plan Examination"
                  }
                }
                """, "application/json"),
            new Response("""
                [
                  {
                    "id": "source-1",
                    "sourceNo": "1",
                    "type": "computation",
                    "body": {
                      "id": "body-1",
                      "name": "computation",
                      "extension": "pdf",
                      "size": 4,
                      "type": "application/pdf"
                    }
                  },
                  {
                    "id": "source-2",
                    "sourceNo": "2",
                    "type": "plan",
                    "body": {
                      "id": "body-2",
                      "name": "plan_map",
                      "extension": "pdf",
                      "size": 4,
                      "type": "application/pdf"
                    }
                  }
                ]
                """, "application/json"),
            new Response("PDF!", "application/pdf"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = Session();
        var selected = new SelectedInnolaTransaction("task-1", "tx-1", "TR100000206", "Assign Computation Task", "parcel_workflow", DateTimeOffset.UtcNow);

        var detail = await service.GetTransactionDetailAsync(session, selected);
        var content = await service.GetAttachmentContentAsync(session, detail.Detail!, detail.Detail!.Attachments[0]);

        TestAssert.True(detail.Success, "Detail should load from transaction source fallback.");
        TestAssert.Equal(2, detail.Detail?.Attachments.Count ?? -1, "Source fallback attachment count mismatch.");
        TestAssert.Equal("computation.pdf", detail.Detail!.Attachments[0].FileName, "Body name and extension should form file name.");
        TestAssert.Equal(SourceRole.ComputationSource, detail.Detail.Attachments[0].SourceRole, "Computation role mismatch.");
        TestAssert.Equal(SourceRole.PlanMapReference, detail.Detail.Attachments[1].SourceRole, "Plan role mismatch.");
        TestAssert.True(content.Success, "Source body content should download.");
        TestAssert.Equal(3, handler.Requests.Count, "Task detail, source list, and body download endpoints should be called.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.Contains("/api/rest/administrative/ladmobjects/getbytransaction", StringComparison.Ordinal), "Source list endpoint mismatch.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.Contains("typeKeyId=source", StringComparison.Ordinal), "Source list should request source LADM objects.");
        TestAssert.True(handler.Requests[2].Uri.AbsoluteUri.Contains("/api/v4/rest/source/download?bodyId=body-1", StringComparison.Ordinal), "Body download endpoint mismatch.");
    }

    public static async Task LiveDetailFallsBackToApplicationIdWhenTransactionSourcesAreEmpty()
    {
        var handler = new SequenceHandler(
            new Response("""
                {
                  "id": "task-1",
                  "name": "Assign Computation Task",
                  "transactionId": "019eb89a-3216-7ad8-b35e-fd5325b42602",
                  "transactionCode": "PE",
                  "transaction": {
                    "id": "019eb89a-3216-7ad8-b35e-fd5325b42602",
                    "transactionNo": "100000206",
                    "transactionType": "Plan Examination"
                  }
                }
                """, "application/json"),
            new Response("[]", "application/json"),
            new Response("[]", "application/json"),
            new Response("""
                [
                  {
                    "id": "source-1",
                    "sourceNo": "1",
                    "type": "computation",
                    "body": {
                      "id": "body-1",
                      "name": "computation",
                      "extension": "pdf",
                      "size": 4,
                      "type": "application/pdf"
                    }
                  }
                ]
                """, "application/json"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = Session();
        var selected = new SelectedInnolaTransaction(
            "task-1",
            "019eb89a-3216-7ad8-b35e-fd5325b42602",
            "100000206",
            "Assign Computation Task",
            "parcel_workflow",
            DateTimeOffset.UtcNow,
            "019eb89a-320c-7b1b-ac98-10c056bf9c50");

        var detail = await service.GetTransactionDetailAsync(session, selected);

        TestAssert.True(detail.Success, "Detail should load from application source fallback.");
        TestAssert.Equal(1, detail.Detail?.Attachments.Count ?? -1, "Application fallback attachment count mismatch.");
        TestAssert.Equal("computation.pdf", detail.Detail!.Attachments[0].FileName, "Application fallback should map body file.");
        TestAssert.Equal(4, handler.Requests.Count, "Task detail, scanning application source list, transaction source list, and application source list should be called.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.Contains("/api/rest/scanning/application/019eb89a-320c-7b1b-ac98-10c056bf9c50", StringComparison.Ordinal), "First source fallback should use scanning application detail.");
        TestAssert.True(handler.Requests[2].Uri.AbsoluteUri.Contains("transactionId=019eb89a-3216-7ad8-b35e-fd5325b42602", StringComparison.Ordinal), "Second source lookup should use transaction id.");
        TestAssert.True(handler.Requests[3].Uri.AbsoluteUri.Contains("transactionId=019eb89a-320c-7b1b-ac98-10c056bf9c50", StringComparison.Ordinal), "Third source lookup should use application id.");
    }

    public static async Task LiveDetailFallsBackToScanningApplicationSourcesAndDownloadsBody()
    {
        var handler = new SequenceHandler(
            new Response("""
                {
                  "id": "task-1",
                  "name": "Assign Computation Task",
                  "transactionId": "019eb89a-3216-7ad8-b35e-fd5325b42602",
                  "transactionCode": "PE",
                  "transaction": {
                    "id": "019eb89a-3216-7ad8-b35e-fd5325b42602",
                    "transactionNo": "100000206",
                    "transactionType": "Plan Examination"
                  }
                }
                """, "application/json"),
            new Response("""
                {
                  "id": "019eb89a-320c-7b1b-ac98-10c056bf9c50",
                  "applicationNo": "NLA26061100206",
                  "sources": [
                    {
                      "id": "source-1",
                      "sourceNo": "26061100206",
                      "type": "source_computation",
                      "body": {
                        "id": "body-1",
                        "name": "computation",
                        "extension": "pdf",
                        "size": 4,
                        "type": "application/pdf"
                      }
                    }
                  ],
                  "transactions": [
                    {
                      "id": "019eb89a-3216-7ad8-b35e-fd5325b42602",
                      "transactionNo": "100000206",
                      "sources": [
                        {
                          "id": "source-2",
                          "sourceNo": "26061100207",
                          "type": "source_plan",
                          "body": {
                            "id": "body-2",
                            "name": "plan_map",
                            "extension": "pdf",
                            "size": 4,
                            "type": "application/pdf"
                          }
                        }
                      ]
                    }
                  ]
                }
                """, "application/json"),
            new Response("PDF!", "application/pdf"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var session = Session();
        var selected = new SelectedInnolaTransaction(
            "task-1",
            "019eb89a-3216-7ad8-b35e-fd5325b42602",
            "100000206",
            "Assign Computation Task",
            "parcel_workflow",
            DateTimeOffset.UtcNow,
            "019eb89a-320c-7b1b-ac98-10c056bf9c50");

        var detail = await service.GetTransactionDetailAsync(session, selected);
        var content = await service.GetAttachmentContentAsync(session, detail.Detail!, detail.Detail!.Attachments[0]);

        TestAssert.True(detail.Success, "Detail should load from scanning application sources.");
        TestAssert.Equal(2, detail.Detail?.Attachments.Count ?? -1, "Scanning application source count mismatch.");
        TestAssert.Equal("computation.pdf", detail.Detail!.Attachments[0].FileName, "Scanning source body name should map.");
        TestAssert.Equal("plan_map.pdf", detail.Detail.Attachments[1].FileName, "Nested transaction source body name should map.");
        TestAssert.True(content.Success, "Scanning source body content should download.");
        TestAssert.Equal(3, handler.Requests.Count, "Task detail, scanning application detail, and source body download should be called.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.Contains("/api/rest/scanning/application/019eb89a-320c-7b1b-ac98-10c056bf9c50", StringComparison.Ordinal), "Scanning application endpoint mismatch.");
        TestAssert.True(handler.Requests[2].Uri.AbsoluteUri.Contains("/api/v4/rest/source/download?bodyId=body-1", StringComparison.Ordinal), "Body download endpoint mismatch.");
    }

    public static async Task LiveDetailKeepsSelectedTransactionNumberWhenDetailOnlyHasApplicationNumber()
    {
        var handler = new SequenceHandler(
            new Response("""
                {
                  "id": "task-1",
                  "name": "Assign Computation Task",
                  "transactionId": "019eb89a-3216-7ad8-b35e-fd5325b42602",
                  "transactionCode": "PE",
                  "application": {
                    "id": "019eb89a-320c-7b1b-ac98-10c056bf9c50",
                    "applicationNo": "NLA26061100206"
                  }
                }
                """, "application/json"),
            new Response("""
                {
                  "id": "019eb89a-320c-7b1b-ac98-10c056bf9c50",
                  "applicationNo": "NLA26061100206",
                  "sources": [
                    {
                      "id": "source-1",
                      "sourceNo": "26061100206",
                      "type": "source_computation",
                      "body": {
                        "id": "body-1",
                        "name": "computation",
                        "extension": "pdf",
                        "size": 4,
                        "type": "application/pdf"
                      }
                    }
                  ]
                }
                """, "application/json"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));
        var selected = new SelectedInnolaTransaction(
            "task-1",
            "019eb89a-3216-7ad8-b35e-fd5325b42602",
            "100000206",
            "Assign Computation Task",
            "parcel_workflow",
            DateTimeOffset.UtcNow,
            "019eb89a-320c-7b1b-ac98-10c056bf9c50");

        var detail = await service.GetTransactionDetailAsync(Session(), selected);

        TestAssert.True(detail.Success, "Detail should load.");
        TestAssert.Equal("100000206", detail.Detail?.TransactionNumber, "Selected transaction number should not be replaced by application number.");
    }

    public static async Task LiveDetailWithoutSourceIdentifiersFailsSafely()
    {
        var handler = new SequenceHandler(
            new Response("""
                {
                  "id": "task-1",
                  "name": "Computation Check",
                  "transaction": {
                    "id": "tx-1",
                    "transactionNo": "TR100000004"
                  },
                  "application": {
                    "applicationNotes": []
                  }
                }
                """, "application/json"),
            new Response("[]", "application/json"));
        var service = new InnolaTransactionDetailService(new HttpClient(handler));

        var result = await service.GetTransactionDetailAsync(
            Session(),
            new SelectedInnolaTransaction("task-1", "tx-1", "TR100000004", "Computation Check", "parcel_workflow", DateTimeOffset.UtcNow));

        TestAssert.True(!result.Success, "Detail should fail when no downloadable source metadata exists.");
        TestAssert.Equal("attachment_metadata_unavailable", result.ErrorCode, "Error code mismatch.");
        TestAssert.True(!result.ErrorMessage!.Contains("token", StringComparison.OrdinalIgnoreCase), "Error should not leak token.");
    }

    private static InnolaSession Session()
    {
        return new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-abc",
            new InnolaUserContext("tester", "Test User", new[] { "survey" }, Array.Empty<string>()),
            null);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<Response> responses;

        public SequenceHandler(params Response[] responses)
        {
            this.responses = new Queue<Response>(responses);
        }

        public List<CapturedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!));
            var response = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, response.ContentType)
            });
        }
    }

    private sealed record Response(string Body, string ContentType);

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri);
}
