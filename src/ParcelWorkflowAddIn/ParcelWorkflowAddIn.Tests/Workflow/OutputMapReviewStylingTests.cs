using ParcelWorkflowAddIn.Workflow.Output;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class OutputMapReviewStylingTests
{
    public static void OrderLayerPathsPlacesParcelFabricBelowReviewOverlays()
    {
        var ordered = OutputMapReviewStyling.OrderLayerPaths(
            new[]
            {
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric"
            });

        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric", ordered[0], "Parcel Fabric layer should load before overlay review layers.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_polygons", ordered[1], "Polygon overlay should load above Parcel Fabric.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_lines", ordered[2], "Line overlay should load above polygon overlay.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_points", ordered[3], "Point overlay should load last.");
    }

    public static void OrderLayerPathsPlacesPolygonsBelowLinesBelowPoints()
    {
        var ordered = OutputMapReviewStyling.OrderLayerPaths(
            new[]
            {
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons"
            });

        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_polygons", ordered[0], "Polygon layer should load first.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_lines", ordered[1], "Line layer should load second.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_points", ordered[2], "Point layer should load last.");
    }

    public static void BuildSuccessMessageUsesNonFabricReviewLanguage()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000206",
            "run-1",
            "2026-06-22T00:00:00Z",
            "tester",
            "hash-1",
            new OutputSummaryPayload(
                "created",
                "normal",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                Array.Empty<string>(),
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                3,
                2,
                1,
                null,
                null,
                ReviewResultOwnership.ApprovedReview),
            Array.Empty<string>(),
            Array.Empty<string>());

        var message = OutputMapReviewStyling.BuildSuccessMessage(summary);

        TestAssert.True(message.Contains("COGO-ready non-fabric review layers", StringComparison.OrdinalIgnoreCase), "Normal mode message should describe the non-fabric review workspace.");
        TestAssert.True(message.Contains("Diagnostics: map load", StringComparison.OrdinalIgnoreCase), "Success message should include the output diagnostics summary.");
    }

    public static void BuildTransactionGroupLayerNameUsesTransactionNumber()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000236",
            "run-group",
            "2026-06-24T00:00:00Z",
            "tester",
            "hash-group",
            new OutputSummaryPayload(
                "created",
                "normal",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                ReviewResultOwnership.ApprovedReview),
            Array.Empty<string>(),
            Array.Empty<string>());

        var groupName = OutputMapReviewStyling.BuildTransactionGroupLayerName(summary);

        TestAssert.Equal("TR 100000236 - Review", groupName, "Transaction review layers should be grouped under the transaction number.");
    }

    public static void BuildSuccessMessagePrefersRootFeatureClassDiagnosticsWhenAvailable()
    {
        var rootDiagnostic = new OutputFeatureClassDiagnostic(
            @"C:\case\output\case.gdb\parcel_lines",
            true,
            80,
            new[]
            {
                new OutputFeatureClassFieldDiagnostic("bearing_txt", true, 76),
                new OutputFeatureClassFieldDiagnostic("distance_txt", true, 76),
                new OutputFeatureClassFieldDiagnostic("length_txt", true, 76),
                new OutputFeatureClassFieldDiagnostic("distance_m", true, 80),
            });

        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000236",
            "run-diagnostic",
            "2026-06-24T00:00:00Z",
            "tester",
            "hash-diagnostic",
            new OutputSummaryPayload(
                "created",
                "parcel_fabric",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                Array.Empty<string>(),
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                87,
                80,
                7,
                null,
                null,
                ReviewResultOwnership.ApprovedReview,
                null,
                true,
                true,
                "prefer_computed",
                "fabric",
                76,
                76,
                0,
                true,
                76,
                true,
                76,
                0,
                rootDiagnostic,
                null,
                true,
                true,
                true,
                true,
                76,
                80),
            Array.Empty<string>(),
            Array.Empty<string>());

        var message = OutputMapReviewStyling.BuildSuccessMessage(summary);

        TestAssert.True(message.Contains("root bearing_txt present (76)", StringComparison.OrdinalIgnoreCase), "Success message should report root feature class bearing diagnostics.");
        TestAssert.True(message.Contains("root distance_txt present (76)", StringComparison.OrdinalIgnoreCase), "Success message should report root feature class distance diagnostics.");
        TestAssert.True(message.Contains("source root parcel_lines + fabric review", StringComparison.OrdinalIgnoreCase), "Success message should distinguish fabric map load from root output diagnostics.");
    }

    public static void ParcelFabricModeReturnsFabricLayerPlusReviewOverlays()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000206",
            "run-2",
            "2026-06-23T00:00:00Z",
            "tester",
            "hash-2",
            new OutputSummaryPayload(
                "created",
                "parcel_fabric",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                new[]
                {
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric",
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\Points",
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\Connection Lines"
                },
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                @"C:\case\output\case.gdb\parcel_fabric_dataset",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\Points",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\Connection Lines",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\compute_review",
                "true",
                @"C:\case\output\case.gdb\parcel_fabric_dataset",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric",
                "record-1",
                null,
                "compute_review",
                1,
                2,
                3,
                3,
                2,
                1,
                null,
                null,
                ReviewResultOwnership.ApprovedReview),
            Array.Empty<string>(),
            Array.Empty<string>());

        var service = new OutputSummaryPersistenceService();
        var paths = service.GetMapLayerPaths(summary);

        TestAssert.Equal(4, paths.Count, "Parcel Fabric review mode should return the fabric layer plus three overlay layers.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric", paths[0], "Fabric layer should remain the base layer.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_polygons", paths[1], "Polygon overlay should come from the root review outputs.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_lines", paths[2], "Line overlay should come from the root review outputs.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_points", paths[3], "Point overlay should come from the root review outputs.");
    }

    public static void EnterpriseParcelFabricModeReturnsPublishedFabricTargetsPlusOverlays()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000206",
            "run-3",
            "2026-06-23T00:00:00Z",
            "tester",
            "hash-3",
            new OutputSummaryPayload(
                "created",
                "enterprise_parcel_fabric",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                Array.Empty<string>(),
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                3,
                2,
                1,
                null,
                null,
                ReviewResultOwnership.ApprovedReview,
                new EnterpriseWorkingPublishSummary(
                    "published",
                    "Published.",
                    "2026-06-23T00:00:00Z",
                    "tester",
                    "transaction_number",
                    "100000206",
                    "parcel_workflow_compute",
                    "spatial_review_pending",
                    "txn-1",
                    "100000206",
                    "task-1",
                    "Assign Computation Task",
                    "tester",
                    "reviewers",
                    "2026-06-23T00:00:00Z",
                    new[]
                    {
                        new EnterpriseWorkingPublishedLayer("fabric", @"https://fabric.local/server/rest/services/Fabric/FeatureServer/0", 1, false),
                        new EnterpriseWorkingPublishedLayer("parcels", @"https://fabric.local/server/rest/services/Fabric/FeatureServer/3", 1, false),
                        new EnterpriseWorkingPublishedLayer("records", @"https://fabric.local/server/rest/services/Fabric/FeatureServer/5", 1, false)
                    },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>())),
            Array.Empty<string>(),
            Array.Empty<string>());

        var service = new OutputSummaryPersistenceService();
        var paths = service.GetMapLayerPaths(summary);

        TestAssert.Equal(5, paths.Count, "Enterprise Parcel Fabric mode should return service targets plus local overlays.");
        TestAssert.Equal(@"https://fabric.local/server/rest/services/Fabric/FeatureServer/0", paths[0], "Fabric target should load first.");
        TestAssert.Equal(@"https://fabric.local/server/rest/services/Fabric/FeatureServer/3", paths[1], "Parcel target should load second.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_polygons", paths[2], "Polygon overlay should remain available.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_lines", paths[3], "Line overlay should remain available.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_points", paths[4], "Point overlay should remain available.");
    }

    public static void BuildSuccessMessageUsesEnterpriseParcelFabricLanguage()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000206",
            "run-4",
            "2026-06-23T00:00:00Z",
            "tester",
            "hash-4",
            new OutputSummaryPayload(
                "created",
                "enterprise_parcel_fabric",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                Array.Empty<string>(),
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                3,
                2,
                1,
                null,
                null,
                ReviewResultOwnership.ApprovedReview),
            Array.Empty<string>(),
            Array.Empty<string>());

        var message = OutputMapReviewStyling.BuildSuccessMessage(summary);

        TestAssert.True(message.Contains("Working Parcel Fabric review layers", StringComparison.OrdinalIgnoreCase), "Enterprise Parcel Fabric mode should describe the working Parcel Fabric review surface.");
        TestAssert.True(message.Contains("Diagnostics: map load", StringComparison.OrdinalIgnoreCase), "Fabric success message should include the output diagnostics summary.");
    }
}
