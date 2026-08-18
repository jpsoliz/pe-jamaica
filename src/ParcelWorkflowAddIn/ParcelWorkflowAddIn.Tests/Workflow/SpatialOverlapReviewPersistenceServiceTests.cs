using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.SpatialReview;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class SpatialOverlapReviewPersistenceServiceTests
{
    public static void OverlapReviewServiceBlocksWhenNoTargetsAreConfigured()
    {
        var service = new ArcGisSpatialOverlapReviewService();
        var request = new SpatialOverlapReviewRequest(
            SpatialOverlapReviewScopes.Compute,
            "tx-400",
            "100000400",
            "TR 100000400 - Review",
            new[] { "parcel_polygons" },
            Array.Empty<SpatialOverlapReviewTargetLayer>(),
            0.2);

        var result = service.RunAsync(request).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Overlap review should block when no target layers are configured.");
        TestAssert.Equal("No overlap-review target layers are enabled in settings.", result.Message, "Blocked reason should explain the missing target configuration.");
    }

    public static void OverlapReviewServiceBlocksWhenNoActiveMapExists()
    {
        var service = new ArcGisSpatialOverlapReviewService();
        var request = new SpatialOverlapReviewRequest(
            SpatialOverlapReviewScopes.Compare,
            "tx-401",
            "100000401",
            "TR 100000401 - Review",
            new[] { "compare_review" },
            new[]
            {
                new SpatialOverlapReviewTargetLayer(
                    "legal",
                    "legal_parcels",
                    "Legal Parcels",
                    "Parcels",
                    "https://example/FeatureServer/0",
                    CompareEnterpriseCadasterSourceSettings.Disabled("Legal Parcels") with
                    {
                        Enabled = true,
                        DisplayName = "legal_parcels",
                        SublayerName = "Parcels",
                        LayerUrl = "https://example/FeatureServer/0"
                    })
            },
            0.2);

        var result = service.RunAsync(request).GetAwaiter().GetResult();

        TestAssert.True(!result.Success, "Overlap review should block when ArcGIS Pro has no active map.");
        TestAssert.Equal("Run Overlap Review requires an active ArcGIS Pro map.", result.Message, "Blocked reason should identify the missing active map.");
    }

    public static void SaveAndLoadComputeArtifactRoundTrips()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000400");
        var service = new SpatialOverlapReviewPersistenceService();
        var document = CreateDocument(SpatialOverlapReviewScopes.Compute, "100000400", "tx-400", "compute group");

        var path = service.Save(layout, document);
        var reloaded = service.Load(layout, SpatialOverlapReviewScopes.Compute);

        TestAssert.True(File.Exists(path), "Compute overlap review artifact should be written.");
        TestAssert.True(reloaded is not null, "Compute overlap review artifact should reload.");
        TestAssert.Equal(SpatialOverlapReviewScopes.Compute, reloaded!.Scope, "Scope should persist.");
        TestAssert.Equal("100000400", reloaded.TransactionNumber, "Transaction number should persist.");
        TestAssert.Equal(1, reloaded.Records.Count, "Overlap records should persist.");
        TestAssert.Equal("legal", reloaded.Layers[0].LayerRole, "Layer role should persist.");
        TestAssert.Equal("group-001", reloaded.Records[0].OverlapGroupId, "Overlap group id should persist.");
        TestAssert.Equal("group-001:1", reloaded.Records[0].OverlapId, "Overlap id should persist.");
        TestAssert.Equal("not_requested", reloaded.Records[0].EnrichmentStatus, "Enrichment status should persist.");
        TestAssert.Equal(1, reloaded.Snapshots?.Count ?? 0, "Snapshot references should persist.");
    }

    public static void SaveCompareArtifactOverwritesInsteadOfAppending()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path, "100000401");
        var service = new SpatialOverlapReviewPersistenceService();
        var first = CreateDocument(SpatialOverlapReviewScopes.Compare, "100000401", "tx-401", "compare group");
        var second = first with
        {
            Summary = first.Summary with
            {
                Message = "Second write wins.",
                OverlapRecordCount = 0,
                NoOverlapLayerCount = 2
            },
            Records = Array.Empty<SpatialOverlapReviewRecord>()
        };

        var path = service.Save(layout, first);
        service.Save(layout, second);
        var reloaded = service.Load(layout, SpatialOverlapReviewScopes.Compare);
        var json = File.ReadAllText(path);

        TestAssert.True(reloaded is not null, "Compare overlap review artifact should reload after overwrite.");
        TestAssert.Equal("Second write wins.", reloaded!.Summary.Message, "Later save should replace the previous document.");
        TestAssert.Equal(0, reloaded.Records.Count, "Overwritten compare artifact should not keep stale overlap rows.");
        TestAssert.True(!json.Contains("\"feature_identity\": \"lot-123\"},\r\n  {\r\n    \"layer_role\"", StringComparison.Ordinal),
            "Artifact file should be rewritten cleanly instead of appending stale JSON rows.");
    }

    private static SpatialOverlapReviewDocument CreateDocument(
        string scope,
        string transactionNumber,
        string transactionId,
        string groupLayerName)
    {
        return new SpatialOverlapReviewDocument(
            "spatial-overlap-review/v1",
            scope,
            transactionId,
            transactionNumber,
            "2026-08-17T00:00:00Z",
            groupLayerName,
            "review_layer",
            1250.25,
            new SpatialOverlapReviewSummary(
                SpatialOverlapReviewStatuses.Ready,
                "Overlap review complete.",
                2,
                2,
                0,
                1,
                1),
            new[]
            {
                new SpatialOverlapReviewLayerResult(
                    "legal",
                    "Legal Parcels",
                    "legal_parcels",
                    true,
                    "overlap",
                    "1 overlap found.",
                    1,
                    false,
                    "Legal Parcels")
            },
            new[]
            {
                new SpatialOverlapReviewRecord(
                    "legal",
                    "Legal Parcels",
                    "legal_parcels",
                    "overlap",
                    "lot-123",
                    "45",
                    "PID-100",
                    "PID-100",
                    "123",
                    "45",
                    "LV-100",
                    "PE-100",
                    "DP-100",
                    "R-100",
                    115.5,
                    9.24,
                    "group-001",
                    "group-001:1",
                    "not_requested")
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new SpatialOverlapReviewSnapshotRef(
                    "group-001",
                    "group-001:1",
                    "Legal overlap snapshot",
                    "reports/overlap/legal-overlap-001.png",
                    "ready")
            });
    }

    private static CaseFolderLayout CreateLayout(string root, string transactionNumber)
    {
        var layout = CaseFolderLayout.For(root, transactionNumber);
        Directory.CreateDirectory(layout.RootDirectory);
        Directory.CreateDirectory(layout.SourceDirectory);
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        Directory.CreateDirectory(layout.ReportsDirectory);
        Directory.CreateDirectory(layout.LogsDirectory);
        return layout;
    }
}
