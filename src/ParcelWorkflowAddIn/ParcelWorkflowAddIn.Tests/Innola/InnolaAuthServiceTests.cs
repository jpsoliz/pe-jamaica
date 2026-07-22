using ParcelWorkflowAddIn.Innola;
using System.Net;
using System.Text;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaAuthServiceTests
{
    public static async Task LoginMapsUserAndGroupContextFromResponse()
    {
        var handler = new CapturingSequenceHttpMessageHandler(
            """
            {
              "access-token": "token-abc"
            }
            """,
            """
            {
              "userName": "jane.user",
              "fullName": "Jane User",
              "groups": ["survey", "qc"],
              "roles": ["operator"]
            }
            """);
        var http = new HttpClient(handler);
        var service = new InnolaAuthService(http);

        var result = await service.LoginAsync("https://eltrs.innola-solutions.com/", "jane.user", "session-password");

        TestAssert.True(result.Success, "Login response should be mapped as success.");
        TestAssert.Equal("token-abc", result.Session?.AccessToken, "Access token mismatch.");
        TestAssert.Equal("jane.user", result.Session?.User.Username, "Username mismatch.");
        TestAssert.Equal("Jane User", result.Session?.User.DisplayName, "Display name mismatch.");
        TestAssert.Equal(2, result.Session?.User.Groups.Count ?? -1, "Group count mismatch.");
        TestAssert.True(result.Session!.User.Groups.Contains("survey"), "Survey group should be mapped.");
        TestAssert.True(result.Session.User.Groups.Contains("qc"), "QC group should be mapped.");
        TestAssert.Equal(1, result.Session.User.Roles.Count, "Role count mismatch.");
        TestAssert.Equal("operator", result.Session.User.Roles[0], "Role mismatch.");
        TestAssert.Equal(2, handler.Requests.Count, "Auth should call login and current user details.");
        TestAssert.Equal(HttpMethod.Post, handler.Requests[0].Method, "Login should use POST.");
        TestAssert.True(handler.Requests[0].Uri.AbsoluteUri.EndsWith("/api/rest/authenticate", StringComparison.Ordinal), "Login endpoint mismatch.");
        TestAssert.True(handler.Requests[0].Body.Contains("\"createSession\":true", StringComparison.Ordinal), "Login should request session creation.");
        TestAssert.True(handler.Requests[0].Body.Contains("\"generateAccessToken\":true", StringComparison.Ordinal), "Login should request an API access token.");
        TestAssert.True(handler.Requests[0].Body.Contains("\"module\":\"default\"", StringComparison.Ordinal), "Login should include module.");
        TestAssert.True(handler.Requests[0].Body.Contains("\"version\":\"1\"", StringComparison.Ordinal), "Login should include version.");
        TestAssert.Equal(HttpMethod.Get, handler.Requests[1].Method, "Current user should use GET.");
        TestAssert.True(handler.Requests[1].Uri.AbsoluteUri.EndsWith("/api/rest/currentUserDetails", StringComparison.Ordinal), "Current user endpoint mismatch.");
    }

    public static async Task FailedLiveLoginWritesSanitizedDiagnosticTrace()
    {
        using var tempRoot = new TempDirectory();
        var handler = new StatusCodeHttpMessageHandler(HttpStatusCode.Unauthorized);
        var trace = new InnolaLoginTraceService(
            tempRoot.Path,
            () => DateTimeOffset.Parse("2026-07-22T10:15:00Z"));
        var certificateSettings = new InnolaClientCertificateSettings(
            true,
            "CurrentUser",
            "My",
            "certificate-that-should-not-exist-in-test",
            null,
            false,
            false);
        var service = new InnolaAuthService(new HttpClient(handler), trace, certificateSettings);

        var result = await service.LoginAsync("https://eltrs.innola-solutions.com/", "jane.user", "secret-password");

        TestAssert.False(result.Success, "Unauthorized login should fail.");
        TestAssert.True(File.Exists(trace.TracePath), "Failed login should write a target-machine diagnostic trace.");
        var traceText = File.ReadAllText(trace.TracePath);
        TestAssert.True(traceText.Contains("\"step\": \"client_certificate\"", StringComparison.Ordinal), "Login trace should include certificate diagnostics.");
        TestAssert.True(traceText.Contains("\"status\": \"not_found\"", StringComparison.Ordinal), "Login trace should show that the configured test certificate was not found.");
        TestAssert.True(traceText.Contains("\"step\": \"login_http_response\"", StringComparison.Ordinal), "Login trace should include HTTP status diagnostics.");
        TestAssert.True(traceText.Contains("\"status_code\": \"401\"", StringComparison.Ordinal), "Login trace should include sanitized HTTP status code.");
        TestAssert.False(traceText.Contains("secret-password", StringComparison.Ordinal), "Login trace must not write the password.");
    }

    public static async Task SessionManagerRaisesLoginChangeOnCallerContext()
    {
        var context = new TrackingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var auth = new AsyncFakeAuthService();
            var manager = new InnolaSessionManager(auth);
            var callbackContextMatches = false;
            manager.SessionChanged += (_, _) =>
            {
                if (manager.Status == InnolaSessionStatus.LoggedIn)
                {
                    callbackContextMatches = ReferenceEquals(SynchronizationContext.Current, context);
                }
            };

            await manager.LoginAsync("https://eltrs.innola-solutions.com/", "tester", "password");

            TestAssert.True(callbackContextMatches, "Logged-in session change should resume on caller synchronization context.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    public static async Task MockAuthEnablesDryRunLogin()
    {
        var service = new MockInnolaAuthService();

        var result = await service.LoginAsync("eltrs.innola-solutions.com", "dry.run.user", string.Empty);

        TestAssert.True(result.Success, "Mock login should succeed for dry-run testing.");
        TestAssert.Equal("https://eltrs.innola-solutions.com/", result.Session?.ServerUrl, "Mock login should normalize server URL.");
        TestAssert.Equal("dry.run.user", result.Session?.User.Username, "Mock login user mismatch.");
        TestAssert.True(result.Session!.User.Groups.Contains("survey"), "Mock login should include a survey group.");
        TestAssert.True(!string.IsNullOrWhiteSpace(result.Session.AccessToken), "Mock login should produce an in-memory token.");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string responseBody;

        public FakeHttpMessageHandler(string responseBody)
        {
            this.responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class StatusCodeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;

        public StatusCodeHttpMessageHandler(HttpStatusCode statusCode)
        {
            this.statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                ReasonPhrase = "Unauthorized"
            });
        }
    }

    private sealed class CapturingSequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> responseBodies;

        public CapturingSequenceHttpMessageHandler(params string[] responseBodies)
        {
            this.responseBodies = new Queue<string>(responseBodies);
        }

        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBodies.Count == 0 ? "{}" : responseBodies.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed class AsyncFakeAuthService : IInnolaAuthService
    {
        public InnolaSession? CurrentSession { get; private set; }

        public async Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            CurrentSession = new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                serverUrl,
                username,
                password,
                "token",
                new InnolaUserContext(username, username, Array.Empty<string>(), Array.Empty<string>()),
                null);
            return InnolaLoginResult.Succeeded(CurrentSession);
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            CurrentSession = null;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
