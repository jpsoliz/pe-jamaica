using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareWorkingGeometryServiceTests
{
    public static void TransactionNumberScopeBuildsNormalizedQuery()
    {
        var service = new CompareWorkingGeometryService(
            () => CreateSettings("transaction_number"),
            new CapturingMapIntegrationService(CompareMapIntegrationResult.Loaded("loaded", Array.Empty<string>(), null, 1)));

        var result = service.LoadAsync(CreateTransaction(transactionNumber: "TR100000674")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Geometry load should be ready.");
        TestAssert.Equal("100000674", result.Plan?.ScopeValue, "Transaction number should normalize away the TR prefix.");
        TestAssert.Equal("transaction_number = '100000674'", result.Plan?.DefinitionQuery, "Definition query mismatch.");
        TestAssert.Equal(3, result.Plan?.Layers.Count ?? 0, "Compare should request polygons, lines, and points.");
    }

    public static void NumericTransactionNumberScopeUsesNumericValue()
    {
        var service = new CompareWorkingGeometryService(
            () => CreateSettings("transaction_number"),
            new CapturingMapIntegrationService(CompareMapIntegrationResult.Loaded("loaded", Array.Empty<string>(), null, 1)));

        var result = service.LoadAsync(CreateTransaction(transactionNumber: "100000674")).GetAwaiter().GetResult();

        TestAssert.Equal("100000674", result.Plan?.ScopeValue, "Numeric transaction number should remain unchanged.");
        TestAssert.Equal("transaction_number = '100000674'", result.Plan?.DefinitionQuery, "Definition query mismatch.");
    }

    public static void TransactionIdScopeUsesPublishedTransactionNumberValue()
    {
        var map = new CapturingMapIntegrationService(CompareMapIntegrationResult.Loaded("loaded", Array.Empty<string>(), null, 1));
        var service = new CompareWorkingGeometryService(() => CreateSettings("transaction_id"), map);

        var result = service.LoadAsync(CreateTransaction(transactionId: "019e-task", transactionNumber: "TR100000674")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Geometry load should be ready.");
        TestAssert.Equal("transaction_id", result.Plan?.ScopeField, "Configured scope field should be used.");
        TestAssert.Equal("100000674", result.Plan?.ScopeValue, "Jamaica working_review transaction_id stores the published transaction number, not the Innola UUID.");
        TestAssert.Equal("transaction_id = '100000674'", result.Plan?.DefinitionQuery, "Definition query mismatch.");
        TestAssert.Equal("transaction_id = '100000674'", map.LastPlan?.Layers[0].DefinitionQuery, "Map layer query mismatch.");
    }

    public static void ServiceRootDerivesPortalUrlForAuthentication()
    {
        var service = new CompareWorkingGeometryService(
            () => CreateSettings("transaction_id", serviceRoot: "https://jm-gis.innola-solutions.com/server/rest"),
            new CapturingMapIntegrationService(CompareMapIntegrationResult.Loaded("loaded", Array.Empty<string>(), null, 1)));

        var result = service.LoadAsync(CreateTransaction(transactionId: "100000668", transactionNumber: "TR100000668")).GetAwaiter().GetResult();

        TestAssert.True(result.Success, "Geometry load should be ready.");
        TestAssert.Equal("https://jm-gis.innola-solutions.com/portal", result.Plan?.PortalUrl, "Compare auth should use portal URL derived from service root.");
        TestAssert.Equal("transaction_id = '100000668'", result.Plan?.DefinitionQuery, "Transaction id scope query mismatch.");
    }

    public static void NoPolygonResultBlocksCompareApproval()
    {
        var service = new CompareWorkingGeometryService(
            () => CreateSettings("transaction_number"),
            new CapturingMapIntegrationService(CompareMapIntegrationResult.NoPolygons("No working polygons.")));

        var result = service.LoadAsync(CreateTransaction()).GetAwaiter().GetResult();
        var workspace = new CompareWorkspaceLoadState(
            CompareDocumentLoadState.Loaded("Documents ready.", @"C:\cases\TR100000674"),
            result);

        TestAssert.Equal(CompareGeometryLoadStatus.NoPolygons, result.Status, "No polygons should produce a blocking geometry state.");
        TestAssert.True(result.BlocksApproval, "No polygons should block Compare approval.");
        TestAssert.False(workspace.CanApproveCompare, "Workspace approval should be disabled without polygons.");
    }

    public static void ActiveMapUnavailableIsRetryableAndKeepsDocumentsAvailable()
    {
        var service = new CompareWorkingGeometryService(
            () => CreateSettings("transaction_number"),
            new CapturingMapIntegrationService(CompareMapIntegrationResult.MapUnavailable("No active map.")));

        var result = service.LoadAsync(CreateTransaction()).GetAwaiter().GetResult();
        var workspace = new CompareWorkspaceLoadState(
            CompareDocumentLoadState.Loaded("Documents ready.", @"C:\cases\TR100000674"),
            result);

        TestAssert.Equal(CompareGeometryLoadStatus.MapUnavailable, result.Status, "Active map failure should be tracked separately.");
        TestAssert.True(result.Retryable, "Active map failure should be retryable.");
        TestAssert.True(workspace.Documents.Success, "Document panel should remain available.");
        TestAssert.False(workspace.CanApproveCompare, "Compare approval should stay disabled until geometry loads.");
    }

    public static void MissingWorkingLayerTargetsBlockGeometryLoad()
    {
        var settings = CreateSettings("transaction_number") with
        {
            EnterpriseWorkingReview = CreateWorkingReviewSettings(
                "transaction_number",
                new EnterpriseWorkingLayerTargets("https://example/points", "https://example/lines", null, null, null))
        };
        var service = new CompareWorkingGeometryService(() => settings);

        var result = service.LoadAsync(CreateTransaction()).GetAwaiter().GetResult();

        TestAssert.Equal(CompareGeometryLoadStatus.SettingsInvalid, result.Status, "Missing polygon target should block geometry load.");
        TestAssert.True(result.BlocksApproval, "Invalid geometry settings should block approval.");
        TestAssert.True(result.Message.Contains("layer targets", StringComparison.OrdinalIgnoreCase), "Missing target message should be actionable.");
    }

    public static void ScopeQueryEscapesSingleQuotes()
    {
        var query = CompareWorkingGeometryService.BuildDefinitionQuery("parcel_owner", "O'Neil");

        TestAssert.Equal("parcel_owner = 'O''Neil'", query, "Definition query should escape single quotes.");
    }

    public static void ObjectIdContextQuerySortsIdsAndRejectsUnsafeFields()
    {
        var query = ArcGisCompareMapIntegrationService.BuildObjectIdDefinitionQuery(
            "OBJECTID",
            new long[] { 42, 7, 13 });

        TestAssert.Equal("OBJECTID IN (7,13,42)", query, "Context layer ObjectID filter should be deterministic.");
        var threw = false;
        try
        {
            ArcGisCompareMapIntegrationService.BuildObjectIdDefinitionQuery("OBJECTID; DROP", new long[] { 1 });
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        TestAssert.True(threw, "Unsafe ObjectID fields should not be accepted in definition queries.");
    }

    public static void MapIntegrationExceptionBecomesRetryableFailure()
    {
        var service = new CompareWorkingGeometryService(
            () => CreateSettings("transaction_number"),
            new ThrowingMapIntegrationService());

        var result = service.LoadAsync(CreateTransaction()).GetAwaiter().GetResult();

        TestAssert.Equal(CompareGeometryLoadStatus.MapLoadFailed, result.Status, "Map exception should be converted to map load failure.");
        TestAssert.True(result.Retryable, "Map exception should produce a retryable geometry state.");
        TestAssert.True(result.BlocksApproval, "Map exception should block Compare approval.");
        TestAssert.True(result.Message.Contains("could not be loaded", StringComparison.OrdinalIgnoreCase), "Failure message should be actionable.");
    }

    public static void MapIntegrationClearsExistingCompareGroupBeforeReload()
    {
        var source = File.ReadAllText(FindArcGisCompareMapIntegrationService());

        TestAssert.True(
            source.Contains("ClearGroupLayer(mapView.Map, groupLayer)", StringComparison.Ordinal),
            "Compare map integration should clear the transaction group before re-adding working and cadaster context layers.");
        TestAssert.True(
            source.Contains("RemoveStaleCompareGroups(mapView.Map, groupLayerName)", StringComparison.Ordinal),
            "Compare map integration should remove stale Compare groups from other transactions before loading the active transaction context.");
        TestAssert.True(
            source.Contains("private static void ClearGroupLayer(Map map, GroupLayer groupLayer)", StringComparison.Ordinal),
            "Compare map integration should keep group cleanup explicit and local.");
    }

    private static string FindArcGisCompareMapIntegrationService()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                "Compare",
                "ArcGisCompareMapIntegrationService.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate ArcGisCompareMapIntegrationService.cs from the test output directory.");
    }

    private static InnolaTransactionSettings CreateSettings(string scopeField, string? serviceRoot = null)
    {
        return InnolaTransactionSettings.Default with
        {
            EnterpriseWorkingReview = CreateWorkingReviewSettings(
                scopeField,
                new EnterpriseWorkingLayerTargets(
                    "https://example/FeatureServer/0",
                    "https://example/FeatureServer/1",
                    "https://example/FeatureServer/2",
                    null,
                    "https://example/FeatureServer/3"),
                serviceRoot)
        };
    }

    private static EnterpriseWorkingReviewSettings CreateWorkingReviewSettings(
        string scopeField,
        EnterpriseWorkingLayerTargets layers,
        string? serviceRoot = null)
    {
        return new EnterpriseWorkingReviewSettings(
            true,
            serviceRoot,
            "sidwell_working_review",
            EnterpriseWorkingReviewSettings.PublishBehaviorReplaceTransactionScope,
            EnterpriseWorkingReviewSettings.PublishTimingOnComplete,
            EnterpriseWorkingReviewSettings.RestoreBehaviorPreferLocalThenEnterprise,
            true,
            scopeField,
            layers,
            null);
    }

    private static SelectedInnolaTransaction CreateTransaction(
        string transactionId = "100000674",
        string transactionNumber = "TR100000674")
    {
        return new SelectedInnolaTransaction(
            "task-1",
            transactionId,
            transactionNumber,
            "Compare Survey Plan",
            "Compare",
            DateTimeOffset.Parse("2026-07-14T00:00:00Z"));
    }

    private sealed class CapturingMapIntegrationService : ICompareMapIntegrationService
    {
        private readonly CompareMapIntegrationResult result;

        public CapturingMapIntegrationService(CompareMapIntegrationResult result)
        {
            this.result = result;
        }

        public CompareWorkingGeometryLoadPlan? LastPlan { get; private set; }

        public Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
            CompareWorkingGeometryLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            LastPlan = plan;
            return Task.FromResult(result with
            {
                LoadedLayerUrls = result.LoadedLayerUrls.Count > 0
                    ? result.LoadedLayerUrls
                    : plan.Layers.Select(layer => layer.LayerUrl).ToArray()
            });
        }

        public Task<CompareMapCleanupResult> RemoveTransactionGeometryFromActiveMapAsync(
            string groupLayerName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareMapCleanupResult.Skipped("No cleanup needed in test."));
        }
    }

    private sealed class ThrowingMapIntegrationService : ICompareMapIntegrationService
    {
        public Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
            CompareWorkingGeometryLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Layer factory failed.");
        }

        public Task<CompareMapCleanupResult> RemoveTransactionGeometryFromActiveMapAsync(
            string groupLayerName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareMapCleanupResult.Skipped("No cleanup needed in test."));
        }
    }
}
