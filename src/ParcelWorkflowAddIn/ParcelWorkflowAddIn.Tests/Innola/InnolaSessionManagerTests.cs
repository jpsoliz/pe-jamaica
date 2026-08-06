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

    public static async Task TimedOutLoginReturnsToLoggedOutAndReEnablesLogin()
    {
        var manager = new InnolaSessionManager(new CanceledAuthService());

        var result = await manager.LoginAsync("https://eltrs.innola-solutions.com/", "tester", "secret-password");

        TestAssert.True(!result.Success, "Timed-out login should fail safely.");
        TestAssert.True(!manager.IsLoggedIn, "Timed-out login should not create a session.");
        TestAssert.Equal(InnolaSessionStatus.LoggedOut, manager.Status, "Timed-out login should return to logged out.");
        TestAssert.True(manager.CanOpenLogin, "Login should be available again after timeout.");
        TestAssert.True(!manager.CanOpenTransactionPanel, "Transaction panel should remain gated after timeout.");
        TestAssert.Equal("Login timed out. Check server, certificate, and network.", manager.StatusText, "Timeout status mismatch.");
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

    public static void CompletedTransactionKeepsTerminalStatusAndDisablesActiveCommands()
    {
        var manager = LoggedInManager();
        var selected = new InnolaTransactionRow(
            "task-100000854",
            "txn-100000854",
            "100000854",
            "Compute Survey Plan",
            "parcel_workflow",
            InnolaTransactionStatus.InProgress,
            "Compute Survey Plan",
            "Test User",
            "tester",
            null,
            DateTimeOffset.UtcNow,
            true,
            true,
            null,
            null);
        manager.SelectTransaction(selected, DateTimeOffset.UtcNow);
        manager.MarkTransactionLoaded("100000854", @"C:\Cases\100000854", "2026-07-27T16:39:00Z", false);
        manager.MarkTransactionClaimed("tester", "Test User", "2026-07-27T16:39:01Z", "Transaction is in progress.");

        manager.MarkTransactionCompleted("2026-07-27T16:39:03Z", "Completed. Final package uploaded and transaction closed.");

        TestAssert.Equal(InnolaTransactionLifecycleStatus.Completed, manager.LifecycleStatus, "Completed terminal state should be retained after clearing the active transaction.");
        TestAssert.Equal("Completed. Final package uploaded and transaction closed.", manager.LifecycleStatusText, "Completed status message should remain visible after Finalize.");
        TestAssert.True(!manager.CanSaveProgress, "Suspend/Save should be disabled after completion.");
        TestAssert.True(!manager.CanCancelActiveProcess, "Cancel should be disabled after completion.");
        TestAssert.True(!manager.CanCompleteTransaction, "Finalize should be disabled after completion.");
        TestAssert.True(manager.CanSwitchTransaction, "A completed transaction should allow the user to refresh or select another task.");
        TestAssert.True(!manager.IsTransactionLoaded, "Completed transaction should clear the active loaded transaction.");
    }

    public static async Task RefreshCurrentSessionPreservesLoadedTransaction()
    {
        var refreshedSession = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-refreshed",
            new InnolaUserContext("tester", "Test User", new[] { "survey" }, Array.Empty<string>()),
            null);
        var auth = new FakeAuthService
        {
            LoginResult = InnolaLoginResult.Succeeded(refreshedSession)
        };
        var manager = new InnolaSessionManager(auth);
        manager.ApplySuccessfulSession(refreshedSession with { AccessToken = "token-stale" });
        var selected = new InnolaTransactionRow(
            "task-100000854",
            "txn-100000854",
            "100000854",
            "Compute Survey Plan",
            "parcel_workflow",
            InnolaTransactionStatus.InProgress,
            "Compute Survey Plan",
            "Test User",
            "tester",
            null,
            DateTimeOffset.UtcNow,
            true,
            true,
            null,
            null);
        manager.SelectTransaction(selected, DateTimeOffset.UtcNow);
        manager.MarkTransactionLoaded("100000854", @"C:\Cases\100000854", "2026-07-27T16:39:00Z", false);
        manager.MarkTransactionClaimed("tester", "Test User", "2026-07-27T16:39:01Z", "Transaction is in progress.");

        var result = await manager.RefreshCurrentSessionAsync();

        TestAssert.Equal("token-refreshed", result?.AccessToken, "Refresh should return the refreshed session token.");
        TestAssert.Equal("token-refreshed", manager.CurrentSession?.AccessToken, "Refresh should update the current session token.");
        TestAssert.True(manager.IsTransactionLoaded, "Refresh must preserve the loaded transaction.");
        TestAssert.Equal("100000854", manager.SelectedTransaction?.TransactionNumber, "Refresh must preserve the selected transaction.");
        TestAssert.Equal(InnolaTransactionLifecycleStatus.InProgress, manager.LifecycleStatus, "Refresh must preserve lifecycle ownership state.");
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

    private sealed class CanceledAuthService : IInnolaAuthService
    {
        public InnolaSession? CurrentSession => null;

        public Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException();
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
