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

    public static void ComputedParcelReviewSymbologyUsesPointHaloAndTransparentPolygons()
    {
        TestAssert.Equal(7.0, OutputMapReviewStyling.ParcelPointOutlineSize, "Parcel point black outline size should remain larger than the white fill.");
        TestAssert.Equal(5.0, OutputMapReviewStyling.ParcelPointFillSize, "Parcel point white fill size should sit inside the black outline.");
        TestAssert.True(
            OutputMapReviewStyling.ParcelPointOutlineSize > OutputMapReviewStyling.ParcelPointFillSize,
            "Parcel points should render as black border with white fill.");
        TestAssert.Equal(60, OutputMapReviewStyling.ParcelPolygonLayerTransparencyPercent, "Parcel polygon layer transparency should be set to 60%.");
        TestAssert.Equal(100, OutputMapReviewStyling.ParcelPolygonFillOpacityPercent, "Parcel polygon fill color should remain opaque so layer-level transparency controls the visible result.");
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

    public static void SupportingLayerPathsRouteToSupportingSourcesAndHideByDefault()
    {
        var pointPath = @"C:\case\output\case.gdb\survey_point_layer";
        var cadPath = @"C:\case\output\case.gdb\survey_cad_reference";
        var primaryPath = @"C:\case\output\case.gdb\parcel_lines";

        TestAssert.True(OutputMapReviewStyling.IsSupportingLayerPath(pointPath), "Structured survey point imports should be treated as supporting context.");
        TestAssert.True(OutputMapReviewStyling.IsSupportingLayerPath(cadPath), "DWG reference imports should be treated as supporting context.");
        TestAssert.False(OutputMapReviewStyling.IsSupportingLayerPath(primaryPath), "Primary parcel layers should remain computed review outputs.");

        TestAssert.Equal("Supporting Sources", OutputMapReviewStyling.GetLayerGroupName(pointPath), "Survey point imports should route under the supporting subgroup.");
        TestAssert.Equal("Supporting Sources", OutputMapReviewStyling.GetLayerGroupName(cadPath), "DWG imports should route under the supporting subgroup.");
        TestAssert.Equal("Computed Parcel Review", OutputMapReviewStyling.GetLayerGroupName(primaryPath), "Primary parcel layers should route under the computed subgroup.");

        TestAssert.True(OutputMapReviewStyling.ShouldHideLayerByDefault(pointPath), "Supporting survey point imports should be hidden initially.");
        TestAssert.True(OutputMapReviewStyling.ShouldHideLayerByDefault(cadPath), "Supporting DWG imports should be hidden initially.");
        TestAssert.False(OutputMapReviewStyling.ShouldHideLayerByDefault(primaryPath), "Primary computed review layers should stay visible.");

        TestAssert.False(OutputMapReviewStyling.ShouldIncludeLayerInInitialZoom(pointPath), "Hidden supporting survey point imports should not drive the initial map extent.");
        TestAssert.False(OutputMapReviewStyling.ShouldIncludeLayerInInitialZoom(cadPath), "Hidden supporting DWG imports should not drive the initial map extent.");
        TestAssert.True(OutputMapReviewStyling.ShouldIncludeLayerInInitialZoom(primaryPath), "Primary computed review layers should drive the initial map extent.");
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

    public static void ParcelFabricModePreservesSupportingSurveyAndCadLayers()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000379",
            "run-supporting",
            "2026-07-02T00:00:00Z",
            "tester",
            "hash-supporting",
            new OutputSummaryPayload(
                "created",
                "parcel_fabric",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                new[]
                {
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric",
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric_Points",
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\compute_review_Lines",
                    @"C:\case\output\case.gdb\parcel_fabric_dataset\compute_review",
                    @"C:\case\output\case.gdb\survey_point_layer",
                    @"C:\case\output\case.gdb\survey_cad_reference"
                },
                @"C:\case\output\case.gdb\parcel_points",
                @"C:\case\output\case.gdb\parcel_lines",
                @"C:\case\output\case.gdb\parcel_polygons",
                @"C:\case\output\case.gdb\parcel_fabric_dataset",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric_Points",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\compute_review_Lines",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\compute_review",
                "true",
                @"C:\case\output\case.gdb\parcel_fabric_dataset",
                @"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric",
                "record-1",
                null,
                "compute_review",
                10,
                55,
                46,
                46,
                55,
                10,
                null,
                null,
                ReviewResultOwnership.ApprovedReview),
            Array.Empty<string>(),
            Array.Empty<string>());

        var service = new OutputSummaryPersistenceService();
        var paths = service.GetMapLayerPaths(summary);

        TestAssert.Equal(6, paths.Count, "Parcel Fabric review mode should keep checked supporting sources loadable.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_fabric_dataset\local_parcel_fabric", paths[0], "Fabric layer should remain the base layer.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_polygons", paths[1], "Polygon overlay should come from the root review outputs.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_lines", paths[2], "Line overlay should come from the root review outputs.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_points", paths[3], "Point overlay should come from the root review outputs.");
        TestAssert.Equal(@"C:\case\output\case.gdb\survey_point_layer", paths[4], "Checked structured survey points should remain loadable as supporting context.");
        TestAssert.Equal(@"C:\case\output\case.gdb\survey_cad_reference", paths[5], "Checked DWG reference should remain loadable as supporting context.");
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

    public static void EnterpriseWorkingLayersModeReturnsTransactionScopedWorkingTargets()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000206",
            "run-3b",
            "2026-06-23T00:00:00Z",
            "tester",
            "hash-3b",
            new OutputSummaryPayload(
                "created",
                "enterprise_working_layers",
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
                        new EnterpriseWorkingPublishedLayer("points", @"https://enterprise.local/server/rest/services/Working/FeatureServer/0", 3, false),
                        new EnterpriseWorkingPublishedLayer("lines", @"https://enterprise.local/server/rest/services/Working/FeatureServer/1", 2, false),
                        new EnterpriseWorkingPublishedLayer("polygons", @"https://enterprise.local/server/rest/services/Working/FeatureServer/2", 1, false),
                        new EnterpriseWorkingPublishedLayer("case_index", @"https://enterprise.local/server/rest/services/Working/FeatureServer/3", 1, false)
                    },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>())),
            Array.Empty<string>(),
            Array.Empty<string>());

        var service = new OutputSummaryPersistenceService();
        var paths = service.GetMapLayerPaths(summary);

        TestAssert.Equal(3, paths.Count, "Enterprise working mode should return transaction-scoped spatial working targets.");
        TestAssert.Equal(@"https://enterprise.local/server/rest/services/Working/FeatureServer/2", paths[0], "Working polygons should load first.");
        TestAssert.Equal(@"https://enterprise.local/server/rest/services/Working/FeatureServer/1", paths[1], "Working lines should load second.");
        TestAssert.Equal(@"https://enterprise.local/server/rest/services/Working/FeatureServer/0", paths[2], "Working points should load last.");
    }

    public static void EnterpriseWorkingLayersModeFallsBackToLocalOutputsWhenPublishEvidenceIsMissing()
    {
        var summary = new OutputSummaryDocument(
            "1.0.0",
            "100000416",
            "run-3c",
            "2026-07-01T00:00:00Z",
            "tester",
            "hash-3c",
            new OutputSummaryPayload(
                "created",
                "enterprise_working_layers",
                @"C:\case\output\case.gdb",
                Array.Empty<string>(),
                new[]
                {
                    @"C:\case\output\case.gdb\parcel_points",
                    @"C:\case\output\case.gdb\parcel_lines",
                    @"C:\case\output\case.gdb\parcel_polygons",
                    @"C:\case\output\case.gdb\survey_cad_reference"
                },
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
                46,
                55,
                10,
                null,
                null,
                ReviewResultOwnership.ApprovedReview),
            Array.Empty<string>(),
            Array.Empty<string>());

        var service = new OutputSummaryPersistenceService();
        var paths = service.GetMapLayerPaths(summary);

        TestAssert.Equal(4, paths.Count, "Enterprise working mode should keep local GDB layers loadable when publish evidence is missing.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_points", paths[0], "Local points should remain available.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_lines", paths[1], "Local lines should remain available.");
        TestAssert.Equal(@"C:\case\output\case.gdb\parcel_polygons", paths[2], "Local polygons should remain available.");
        TestAssert.Equal(@"C:\case\output\case.gdb\survey_cad_reference", paths[3], "Optional local supporting layers should remain available.");
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
