using ParcelWorkflowAddIn.Innola;
using System.Net;
using System.Text;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionLifecycleServiceTests
{
    public static async Task LiveLifecycleStartUsesWorkflowStartEndpoint()
    {
        var handler = new SequenceHandler(new Response("""
            {
              "task": {
                "id": "task-1",
                "assignee": "tester"
              }
            }
            """, HttpStatusCode.OK));
        var service = new InnolaTransactionLifecycleService(new HttpClient(handler));

        var result = await service.ClaimAsync(Request());

        TestAssert.True(result.Success, "Start should succeed.");
        TestAssert.Equal("tester", result.OwnerUser, "Owner mismatch.");
        TestAssert.Equal(HttpMethod.Post, handler.Requests[0].Method, "Start should use POST.");
        TestAssert.True(handler.Requests[0].Uri.AbsoluteUri.EndsWith("/api/v4/rest/workflow/tasks/task-1/start", StringComparison.Ordinal), "Start endpoint mismatch.");
    }

    public static async Task LiveLifecycleCompleteResolvesTransitionAndCompletes()
    {
        var handler = new SequenceHandler(
            new Response("""
                [
                  {
                    "transitionId": "flow_approved_enterdata",
                    "isDefault": true
                  }
                ]
                """, HttpStatusCode.OK),
            new Response("", HttpStatusCode.OK));
        var service = new InnolaTransactionLifecycleService(new HttpClient(handler));

        var result = await service.CompleteAsync(Request());

        TestAssert.True(result.Success, "Complete should succeed.");
        TestAssert.Equal(2, handler.Requests.Count, "Transitions and complete endpoints should be called.");
        TestAssert.True(handler.Requests[0].Uri.AbsoluteUri.EndsWith("/api/v4/rest/workflow/tasks/task-1/transitions", StringComparison.Ordinal), "Transitions endpoint mismatch.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.EndsWith("/api/v4/rest/workflow/tasks/task-1/complete?transition=flow_approved_enterdata", StringComparison.Ordinal), "Complete endpoint mismatch.");
    }

    public static async Task LiveLifecycleBusinessFailureIsRedacted()
    {
        var handler = new SequenceHandler(new Response("""
            {
              "success": false,
              "message": "Task is already owned by another user"
            }
            """, HttpStatusCode.OK));
        var service = new InnolaTransactionLifecycleService(new HttpClient(handler));

        var result = await service.ClaimAsync(Request());

        TestAssert.True(!result.Success, "Business failure should fail.");
        TestAssert.True(result.Message!.Contains("another user", StringComparison.OrdinalIgnoreCase), "Business message should be preserved when safe.");
        TestAssert.True(!result.Message.Contains("token", StringComparison.OrdinalIgnoreCase), "Error should not leak token.");
    }

    private static InnolaTransactionLifecycleRequest Request()
    {
        var session = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs-dev.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-abc",
            new InnolaUserContext("tester", "Test User", new[] { "survey" }, Array.Empty<string>()),
            null);
        var transaction = new SelectedInnolaTransaction("task-1", "tx-1", "TR100000004", "Computation Check", "parcel_workflow", DateTimeOffset.UtcNow);
        return new InnolaTransactionLifecycleRequest(session, transaction, @"C:\Temp\Case", "loaded", "test");
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
            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record Response(string Body, HttpStatusCode StatusCode);

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri);
}
