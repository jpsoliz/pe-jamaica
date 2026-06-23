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
}
