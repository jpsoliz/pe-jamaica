using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaSessionManagerTests
{
    public static void SessionStartsLoggedOutWithSafeGates()
    {
        var manager = new InnolaSessionManager(new FakeAuthService());

        TestAssert.True(!manager.IsLoggedIn, "Session should start logged out.");
        TestAssert.True(manager.CanOpenLogin, "Login should be available while logged out.");
        TestAssert.True(manager.CanOpenAbout, "About should be available while logged out.");
        TestAssert.True(manager.CanOpenConfiguration, "Safe configuration should be available while logged out.");
        TestAssert.True(!manager.CanOpenTransactionPanel, "Transaction panel should be gated while logged out.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should be gated while logged out.");
        TestAssert.Equal("Not logged in.", manager.StatusText, "Logged-out status text mismatch.");
    }

    public static async Task SuccessfulLoginStoresSessionInMemoryAndEnablesTransactions()
    {
        var auth = new FakeAuthService
        {
            LoginResult = InnolaLoginResult.Succeeded(new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                "https://eltrs.innola-solutions.com/",
                "tester",
                "secret-password",
                "token-123",
                new InnolaUserContext("tester", "Test User", new[] { "survey" }, Array.Empty<string>()),
                null))
        };
        var manager = new InnolaSessionManager(auth);

        var result = await manager.LoginAsync("https://eltrs.innola-solutions.com/", "tester", "secret-password");

        TestAssert.True(result.Success, "Login should succeed.");
        TestAssert.True(manager.IsLoggedIn, "Session should be logged in.");
        TestAssert.Equal("tester", manager.CurrentUser?.Username, "Current user mismatch.");
        TestAssert.Equal("secret-password", manager.CurrentSession?.SessionPassword, "Password should be retained only in memory for this session.");
        TestAssert.Equal("token-123", manager.CurrentSession?.AccessToken, "Token should be retained only in memory for this session.");
        TestAssert.True(manager.CanOpenTransactionPanel, "Transaction panel should be enabled after login.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow must remain disabled until a transaction is loaded.");
        TestAssert.Equal("Logged in as Test User.", manager.StatusText, "Logged-in status mismatch.");
    }

    public static async Task FailedLoginDoesNotCreateSessionAndKeepsNonSecretError()
    {
        var auth = new FakeAuthService
        {
            LoginResult = InnolaLoginResult.Failure("Login failed. Check user name, password, and server.")
        };
        var manager = new InnolaSessionManager(auth);

        var result = await manager.LoginAsync("https://eltrs.innola-solutions.com/", "tester", "secret-password");

        TestAssert.True(!result.Success, "Login should fail.");
        TestAssert.True(!manager.IsLoggedIn, "Failed login must not create a logged-in session.");
        TestAssert.Equal(null, manager.CurrentSession, "Failed login should not retain session.");
        TestAssert.True(!manager.StatusText.Contains("secret-password", StringComparison.Ordinal), "Status text must not expose password.");
        TestAssert.True(!manager.StatusText.Contains("token", StringComparison.OrdinalIgnoreCase), "Status text must not expose token-like values.");
        TestAssert.Equal("Login failed. Check user name, password, and server.", manager.StatusText, "Failed login message mismatch.");
    }

    public static async Task LogoutClearsSessionSecretsAndDisablesGates()
    {
        var manager = LoggedInManager();

        await manager.LogoutAsync();

        TestAssert.True(!manager.IsLoggedIn, "Logout should clear logged-in state.");
        TestAssert.Equal(null, manager.CurrentSession, "Logout should clear current session.");
        TestAssert.True(!manager.CanOpenTransactionPanel, "Transaction panel should be disabled after logout.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should be disabled after logout.");
        TestAssert.Equal("Not logged in.", manager.StatusText, "Logout status mismatch.");
    }

    public static void SessionExpiryClearsSessionSecretsAndDisablesGates()
    {
        var manager = LoggedInManager();

        manager.ExpireSession();

        TestAssert.True(!manager.IsLoggedIn, "Session expiry should clear logged-in state.");
        TestAssert.Equal(null, manager.CurrentSession, "Session expiry should clear current session.");
        TestAssert.True(!manager.CanOpenTransactionPanel, "Transaction panel should be disabled after expiry.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should be disabled after expiry.");
        TestAssert.Equal("Session expired. Log in again.", manager.StatusText, "Expiry status mismatch.");
    }

    public static async Task SessionSecretsAreNotWrittenToSettingsOrCaseFolderFiles()
    {
        const string secretPassword = "super-secret-session-password";
        const string secretToken = "super-secret-access-token";
        using var tempRoot = new TempDirectory();
        var auth = new FakeAuthService
        {
            LoginResult = InnolaLoginResult.Succeeded(new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                "https://eltrs.innola-solutions.com/",
                "tester",
                secretPassword,
                secretToken,
                new InnolaUserContext("tester", "Test User", new[] { "survey" }, Array.Empty<string>()),
                null))
        };
        var manager = new InnolaSessionManager(auth);

        await manager.LoginAsync("https://eltrs.innola-solutions.com/", "tester", secretPassword);
        var store = new CaseFolderStore(() => new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), () => "run-secret-test");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000999", "tester");

        TestAssert.True(created.Success, "Case creation should succeed for secret leak test.");
        var settingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ParcelWorkflowAddIn",
            "Settings",
            "WorkflowSettings.json"));
        var filesToScan = Directory.GetFiles(created.Layout!.RootDirectory, "*", SearchOption.AllDirectories)
            .Concat(new[] { settingsPath });

        foreach (var file in filesToScan)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            TestAssert.True(!content.Contains(secretPassword, StringComparison.Ordinal), $"Password leaked to file: {file}");
            TestAssert.True(!content.Contains(secretToken, StringComparison.Ordinal), $"Token leaked to file: {file}");
        }
    }

    private static InnolaSessionManager LoggedInManager()
    {
        var manager = new InnolaSessionManager(new FakeAuthService());
        manager.ApplySuccessfulSession(new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-123",
            new InnolaUserContext("tester", "Test User", new[] { "survey" }, Array.Empty<string>()),
            null));
        return manager;
    }

    private sealed class FakeAuthService : IInnolaAuthService
    {
        public InnolaSession? CurrentSession { get; private set; }

        public InnolaLoginResult LoginResult { get; set; } = InnolaLoginResult.Failure("not configured");

        public Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
        {
            CurrentSession = LoginResult.Session;
            return Task.FromResult(LoginResult);
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            CurrentSession = null;
            return Task.CompletedTask;
        }
    }
}
