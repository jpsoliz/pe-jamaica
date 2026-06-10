using ParcelWorkflowAddIn.Innola;
using System.Net;
using System.Text;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaAuthServiceTests
{
    public static async Task LoginMapsUserAndGroupContextFromResponse()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("""
            {
              "value": {
                "accessToken": "token-abc",
                "username": "jane.user",
                "fullName": "Jane User",
                "groups": ["survey", "qc"],
                "roles": ["operator"]
              }
            }
            """));
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
