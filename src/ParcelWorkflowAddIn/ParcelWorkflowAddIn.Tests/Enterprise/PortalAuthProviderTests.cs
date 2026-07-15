using ParcelWorkflowAddIn.Enterprise.PortalAuth;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn.Tests.Enterprise;

public static class PortalAuthProviderTests
{
    public static void EnvironmentProviderPrefersUserTokenOverStaleProcessToken()
    {
        var provider = new EnvironmentPortalAuthProvider(
            EnvironmentPortalAuthProvider.DefaultTokenVariableName,
            (_, target) => target switch
            {
                EnvironmentVariableTarget.Process => "stale-process-token",
                EnvironmentVariableTarget.User => "fresh-user-token",
                EnvironmentVariableTarget.Machine => "machine-token",
                _ => null
            });

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "test")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Environment token lookup should succeed.");
        TestAssert.Equal("fresh-user-token", result.Token, "User token should be preferred over stale process token.");
        TestAssert.True(!result.ToString().Contains("fresh-user-token", StringComparison.Ordinal), "Diagnostics should not expose the token.");
    }

    public static void EnvironmentProviderRejectsTruncatedToken()
    {
        var provider = new EnvironmentPortalAuthProvider(
            EnvironmentPortalAuthProvider.DefaultTokenVariableName,
            (_, target) => target == EnvironmentVariableTarget.User ? "abc123.." : null);

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "test")).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Truncated token should be rejected before REST calls.");
        TestAssert.True(result.ErrorMessage?.Contains("truncated", StringComparison.OrdinalIgnoreCase) == true, "Error should explain truncation.");
        TestAssert.True(result.ErrorMessage?.Contains("abc123", StringComparison.Ordinal) != true, "Error should not expose token text.");
    }

    public static void EnvironmentProviderSkipsTruncatedTokenWhenFallbackIsAvailable()
    {
        var provider = new EnvironmentPortalAuthProvider(
            EnvironmentPortalAuthProvider.DefaultTokenVariableName,
            (_, target) => target switch
            {
                EnvironmentVariableTarget.User => "truncated-token..",
                EnvironmentVariableTarget.Process => "fresh-process-token",
                _ => null
            });

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "test")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Environment token lookup should skip truncated scopes when another token exists.");
        TestAssert.Equal("fresh-process-token", result.Token, "Process token should be used when user token is truncated.");
        TestAssert.True(!result.ToString().Contains("fresh-process-token", StringComparison.Ordinal), "Diagnostics should not expose token text.");
    }

    public static void CompositeProviderUsesFirstSuccessfulProviderAndTracksAttempts()
    {
        var first = new StaticPortalAuthProvider(PortalAuthResult.Failed("arcgis_pro_session", "Not signed in."));
        var second = new StaticPortalAuthProvider(PortalAuthResult.Succeeded("fallback-token", "environment"));
        var provider = new CompositePortalAuthProvider(new IPortalAuthProvider[] { first, second });

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Composite provider should use fallback provider when the first source fails.");
        TestAssert.Equal("fallback-token", result.Token, "Fallback token should be returned.");
        TestAssert.Equal("environment", result.Source, "Successful source should be reported.");
        TestAssert.True(result.AttemptedSources?.Contains("arcgis_pro_session") == true, "Failed source should be recorded.");
        TestAssert.True(result.AttemptedSources?.Contains("environment") == true, "Successful source should be recorded.");
        TestAssert.True(!result.ToString().Contains("fallback-token", StringComparison.Ordinal), "Composite diagnostics should not expose token.");
    }

    public static void CompositeProviderFailureIncludesAttemptedSourcesWithoutSecrets()
    {
        var provider = new CompositePortalAuthProvider(new IPortalAuthProvider[]
        {
            new StaticPortalAuthProvider(PortalAuthResult.Failed("arcgis_pro_session", "Not signed in.")),
            new StaticPortalAuthProvider(PortalAuthResult.Failed("environment", "No token was found."))
        });

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Composite provider should fail when all providers fail.");
        TestAssert.True(result.ErrorMessage?.Contains("arcgis_pro_session", StringComparison.Ordinal) == true, "Error should include attempted ArcGIS Pro source.");
        TestAssert.True(result.ErrorMessage?.Contains("environment", StringComparison.Ordinal) == true, "Error should include attempted environment source.");
        TestAssert.True(!result.ErrorMessage!.Contains("token-value", StringComparison.Ordinal), "Error should not include secret values.");
    }

    public static void ArcGisProProviderReturnsUnavailableOutsidePro()
    {
        var provider = new ArcGisProPortalAuthProvider(() => null);

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Provider should fail cleanly when ArcGIS Pro SDK manager is unavailable.");
        TestAssert.Equal(ArcGisProPortalAuthProvider.SourceName, result.Source, "Failure source should identify ArcGIS Pro session auth.");
        TestAssert.True(result.ErrorMessage?.Contains("not available", StringComparison.OrdinalIgnoreCase) == true, "Failure should explain unavailable manager.");
    }

    public static void ArcGisProProviderUsesActivePortalTokenWhenAvailable()
    {
        var provider = new ArcGisProPortalAuthProvider(() => typeof(FakePortalManager));

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Provider should resolve token from compatible active portal.");
        TestAssert.Equal("pro-session-token", result.Token, "Provider should return active portal token.");
        TestAssert.Equal(ArcGisProPortalAuthProvider.SourceName, result.Source, "Success source should identify ArcGIS Pro session auth.");
        TestAssert.True(!result.ToString().Contains("pro-session-token", StringComparison.Ordinal), "Diagnostics should not expose active portal token.");
    }

    public static void ArcGisProProviderAcceptsActiveRootForConfiguredPortalPath()
    {
        var provider = new ArcGisProPortalAuthProvider(() => typeof(FakeRootPortalManager));

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Provider should accept an active same-host root portal for a configured /portal URL.");
        TestAssert.Equal("pro-session-token", result.Token, "Provider should return active portal token after root-vs-portal normalization.");
        TestAssert.Equal(ArcGisProPortalAuthProvider.SourceName, result.Source, "Success source should identify ArcGIS Pro session auth.");
    }

    public static void ArcGisProProviderFallsBackWhenTokenMethodThrows()
    {
        var provider = new ArcGisProPortalAuthProvider(() => typeof(FakeThrowingTokenPortalManager));

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Provider should keep trying token accessors when one ArcGIS Pro token method throws.");
        TestAssert.Equal("pro-session-token-from-property", result.Token, "Provider should fall back to token properties.");
        TestAssert.Equal(ArcGisProPortalAuthProvider.SourceName, result.Source, "Success source should identify ArcGIS Pro session auth.");
    }

    public static void ArcGisProProviderDoesNotMatchSiblingPortalPath()
    {
        var provider = new ArcGisProPortalAuthProvider(() => typeof(FakeSiblingPortalManager));

        var result = provider.GetTokenAsync(new PortalAuthRequest("https://portal.example/portal", null, "publish")).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Active portal /portal2 should not match configured portal /portal.");
        TestAssert.True(result.ErrorMessage?.Contains("does not match", StringComparison.OrdinalIgnoreCase) == true, "Failure should explain portal mismatch.");
        TestAssert.True(!result.ToString().Contains("pro-session-token", StringComparison.Ordinal), "Diagnostics should not expose active portal token.");
    }

    public static void WorkingLayerPublishServiceUsesPortalAuthProvider()
    {
        var service = new JsonEnterpriseWorkingLayerPublishService(
            () => InnolaTransactionSettings.Default,
            new StaticPortalAuthProvider(PortalAuthResult.Failed("fake-provider", "provider refused request")));
        var method = typeof(JsonEnterpriseWorkingLayerPublishService).GetMethod(
            "PublishFeatureServiceLayerAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            var task = (Task)method!.Invoke(
                service,
                new object[]
                {
                    "https://example.com/server/rest/services/Hosted/working_review/FeatureServer/0",
                    "points",
                    "transaction_number",
                    "100000001",
                    new JsonObject(),
                    CancellationToken.None
                })!;
            task.GetAwaiter().GetResult();
        }
        catch (InvalidOperationException invalidOperation)
        {
            TestAssert.True(invalidOperation.Message.Contains("fake-provider", StringComparison.Ordinal), "Provider diagnostic should be surfaced.");
            TestAssert.True(!invalidOperation.Message.Contains("ARCGIS_PORTAL_TOKEN", StringComparison.Ordinal), "Publish service should not force the environment-token-only guidance.");
            TestAssert.True(!invalidOperation.Message.Contains("token-value", StringComparison.Ordinal), "Provider diagnostics should not expose secret-looking values.");
            return;
        }

        throw new InvalidOperationException("Expected publish service to fail through the injected auth provider.");
    }

    public static void WorkingDispositionServiceUsesPortalAuthProvider()
    {
        var service = new JsonEnterpriseWorkingDispositionService(
            () => InnolaTransactionSettings.Default,
            new StaticPortalAuthProvider(PortalAuthResult.Failed("fake-provider", "provider refused request")));
        var method = typeof(JsonEnterpriseWorkingDispositionService).GetMethod(
            "UpdateFeatureServiceLayerAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            var task = (Task)method!.Invoke(
                service,
                new object[]
                {
                    "https://example.com/server/rest/services/Hosted/working_review/FeatureServer/0",
                    "points",
                    "transaction_number",
                    "100000001",
                    new ComputeReviewDispositionRequest(
                        ComputeReviewDecision.Approved,
                        "ok",
                        "tester",
                        CreateOutputSummary(),
                        CreatePublishSummary(),
                        "output_summary.json",
                        "enterprise_publish.json"),
                    "2026-07-12T00:00:00Z",
                    CancellationToken.None
                })!;
            task.GetAwaiter().GetResult();
        }
        catch (InvalidOperationException invalidOperation)
        {
            TestAssert.True(invalidOperation.Message.Contains("fake-provider", StringComparison.Ordinal), "Provider diagnostic should be surfaced.");
            TestAssert.True(!invalidOperation.Message.Contains("ARCGIS_PORTAL_TOKEN", StringComparison.Ordinal), "Disposition service should not force the environment-token-only guidance.");
            TestAssert.True(!invalidOperation.Message.Contains("token-value", StringComparison.Ordinal), "Provider diagnostics should not expose secret-looking values.");
            return;
        }

        throw new InvalidOperationException("Expected disposition service to fail through the injected auth provider.");
    }

    private static OutputSummaryDocument CreateOutputSummary()
    {
        return new OutputSummaryDocument(
            "output_summary_v1",
            "100000001",
            "run-1",
            "2026-07-12T00:00:00Z",
            "tester",
            "hash",
            new OutputSummaryPayload(
                Status: "succeeded",
                ReviewWorkspaceMode: "enterprise_working_layers",
                ResultGdbPath: null,
                ArtifactPaths: Array.Empty<string>(),
                MapLayerPaths: Array.Empty<string>(),
                PointFeatureClassPath: null,
                LineFeatureClassPath: null,
                PolygonFeatureClassPath: null,
                ReviewDatasetPath: null,
                ReviewLayerPath: null,
                ReviewPointFeatureClassPath: null,
                ReviewLineFeatureClassPath: null,
                ReviewPolygonFeatureClassPath: null,
                ParcelFabricMode: null,
                ParcelFabricDatasetPath: null,
                ParcelFabricLayerPath: null,
                ParcelRecordName: null,
                ParcelRecordId: null,
                ParcelType: null,
                BuiltParcelCount: 1,
                BuiltLineCount: 0,
                BuiltPointCount: 0,
                PointCount: 0,
                LineCount: 0,
                PolygonCount: 0,
                TemplateProjectPath: null,
                TemplateGdbPath: null,
                ReviewResultOwner: "tester"),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static EnterpriseWorkingPublishSummary CreatePublishSummary()
    {
        return new EnterpriseWorkingPublishSummary(
            "succeeded",
            "published",
            "2026-07-12T00:00:00Z",
            "tester",
            "transaction_number",
            "100000001",
            "compute",
            "finalize",
            "100000001",
            "100000001",
            "task-1",
            "PE",
            "tester",
            "group",
            "2026-07-12T00:00:00Z",
            Array.Empty<EnterpriseWorkingPublishedLayer>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private sealed class StaticPortalAuthProvider : IPortalAuthProvider
    {
        private readonly PortalAuthResult result;

        public StaticPortalAuthProvider(PortalAuthResult result)
        {
            this.result = result;
        }

        public Task<PortalAuthResult> GetTokenAsync(PortalAuthRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class FakePortalManager
    {
        public static FakePortalManager Current { get; } = new();

        public FakePortal GetActivePortal()
        {
            return new FakePortal();
        }
    }

    private sealed class FakePortal
    {
        public Uri PortalUri { get; } = new("https://portal.example/portal");

        public string GetToken()
        {
            return "pro-session-token";
        }
    }

    private sealed class FakeRootPortalManager
    {
        public static FakeRootPortalManager Current { get; } = new();

        public FakeRootPortal GetActivePortal()
        {
            return new FakeRootPortal();
        }
    }

    private sealed class FakeRootPortal
    {
        public Uri PortalUri { get; } = new("https://portal.example");

        public string GetToken()
        {
            return "pro-session-token";
        }
    }

    private sealed class FakeThrowingTokenPortalManager
    {
        public static FakeThrowingTokenPortalManager Current { get; } = new();

        public FakeThrowingTokenPortal GetActivePortal()
        {
            return new FakeThrowingTokenPortal();
        }
    }

    private sealed class FakeThrowingTokenPortal
    {
        public Uri PortalUri { get; } = new("https://portal.example/portal");

        public string Token { get; } = "pro-session-token-from-property";

        public string GetToken()
        {
            throw new InvalidOperationException("Token method is unavailable in this test host.");
        }
    }

    private sealed class FakeSiblingPortalManager
    {
        public static FakeSiblingPortalManager Current { get; } = new();

        public FakeSiblingPortal GetActivePortal()
        {
            return new FakeSiblingPortal();
        }
    }

    private sealed class FakeSiblingPortal
    {
        public Uri PortalUri { get; } = new("https://portal.example/portal2");

        public string GetToken()
        {
            return "pro-session-token";
        }
    }
}
