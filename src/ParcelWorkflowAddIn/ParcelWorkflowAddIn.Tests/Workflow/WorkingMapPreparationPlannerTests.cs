using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Maps;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class WorkingMapPreparationPlannerTests
{
    public static void PlannerBuildsOrderedLayerPlanAndDefaultExtentFallback()
    {
        var settings = WorkingMapSettings.Default with
        {
            ReferenceLayers = new[]
            {
                WorkingMapReferenceLayerSettings.Default with
                {
                    Name = "Optional Roads",
                    Url = "https://example.test/roads",
                    Required = false,
                    Visible = false,
                    Order = 20
                },
                WorkingMapReferenceLayerSettings.Default with
                {
                    Name = "Legal_Cadastre",
                    Url = "https://example.test/legal",
                    Required = true,
                    Visible = true,
                    Order = 10
                }
            },
            ParishLookup = WorkingMapParishLookupSettings.Default with
            {
                KnownExtents = new Dictionary<string, WorkingMapExtent>(StringComparer.OrdinalIgnoreCase)
            }
        };
        var detail = new InnolaTransactionDetail(
            "tx-1",
            "100000872",
            "task-1",
            "Compute Survey Plan",
            "parcel_workflow",
            "PE",
            "PE",
            null,
            null,
            null,
            null,
            Array.Empty<InnolaAttachmentMetadata>(),
            "Unknown Parish");

        var plan = WorkingMapPreparationPlanner.Build(settings, detail);

        TestAssert.True(plan.Enabled, "Plan should be enabled.");
        TestAssert.Equal("Jamaica", plan.MapName, "Map name mismatch.");
        TestAssert.Equal(2, plan.Layers.Count, "Layer count mismatch.");
        TestAssert.Equal("Legal_Cadastre", plan.Layers[0].Name, "Required layer should be ordered first.");
        TestAssert.True(plan.Layers[0].Required, "Legal layer should be required.");
        TestAssert.False(plan.Layers[1].Visible, "Optional roads should be hidden.");
        TestAssert.Equal(settings.DefaultExtent, plan.ZoomExtent, "Unmatched parish should fall back to default extent.");
        TestAssert.True(plan.Warnings.Any(warning => warning.Contains("Parish", StringComparison.OrdinalIgnoreCase)), "Unmatched parish should warn.");
    }

    public static void PlannerUsesConfiguredParishExtentWhenAvailable()
    {
        var parishExtent = new WorkingMapExtent("St. Catherine", 4326, -77.25, 17.85, -76.75, 18.25);
        var settings = WorkingMapSettings.Default with
        {
            ParishLookup = WorkingMapParishLookupSettings.Default with
            {
                KnownExtents = new Dictionary<string, WorkingMapExtent>(StringComparer.OrdinalIgnoreCase)
                {
                    ["st catherine"] = parishExtent
                }
            }
        };
        var detail = new InnolaTransactionDetail(
            "tx-1",
            "100000873",
            "task-1",
            "Compute Survey Plan",
            "parcel_workflow",
            "PE",
            "PE",
            null,
            null,
            null,
            null,
            Array.Empty<InnolaAttachmentMetadata>(),
            "St. Catherine");

        var plan = WorkingMapPreparationPlanner.Build(settings, detail);

        TestAssert.Equal(parishExtent, plan.ZoomExtent, "Configured parish extent should be selected.");
        TestAssert.Equal(0, plan.Warnings.Count, "Configured parish should not warn.");
    }

    public static void PlannerUsesDefaultParishExtentFromTransactionParish()
    {
        var detail = new InnolaTransactionDetail(
            "tx-1",
            "100000874",
            "task-1",
            "Compute Survey Plan",
            "parcel_workflow",
            "PE",
            "PE",
            null,
            null,
            null,
            null,
            Array.Empty<InnolaAttachmentMetadata>(),
            "Saint Elizabeth");

        var plan = WorkingMapPreparationPlanner.Build(WorkingMapSettings.Default, detail);

        TestAssert.Equal("St. Elizabeth", plan.ZoomExtent.Name, "Transaction parish should resolve to the built-in parish extent.");
        TestAssert.Equal(0, plan.Warnings.Count, "Known transaction parish should not warn.");
    }

    public static void PlannerFallsBackToJamaicaExtentWhenTransactionParishIsMissing()
    {
        var detail = new InnolaTransactionDetail(
            "tx-1",
            "100000875",
            "task-1",
            "Compute Survey Plan",
            "parcel_workflow",
            "PE",
            "PE",
            null,
            null,
            null,
            null,
            Array.Empty<InnolaAttachmentMetadata>());

        var plan = WorkingMapPreparationPlanner.Build(WorkingMapSettings.Default, detail);

        TestAssert.Equal(WorkingMapSettings.Default.DefaultExtent, plan.ZoomExtent, "Missing transaction parish should use the Jamaica default extent.");
        TestAssert.Equal(0, plan.Warnings.Count, "Missing transaction parish should not warn.");
    }

    public static void PlannerRejectsRequiredLayerWithoutUrlAndWarnsForOptionalLayer()
    {
        var settings = WorkingMapSettings.Default with
        {
            ReferenceLayers = new[]
            {
                WorkingMapReferenceLayerSettings.Default with
                {
                    Name = "Legal_Cadastre",
                    Url = "",
                    Required = true,
                    Order = 10
                },
                WorkingMapReferenceLayerSettings.Default with
                {
                    Name = "Fishing Beaches",
                    Url = "",
                    Required = false,
                    Order = 20
                }
            }
        };

        var plan = WorkingMapPreparationPlanner.Build(settings, null);

        TestAssert.False(plan.Success, "Required missing URL should block preparation.");
        TestAssert.True(plan.Blockers.Any(blocker => blocker.Contains("Legal_Cadastre", StringComparison.OrdinalIgnoreCase)), "Required layer blocker should name the layer.");
        TestAssert.True(plan.Warnings.Any(warning => warning.Contains("Fishing Beaches", StringComparison.OrdinalIgnoreCase)), "Optional layer warning should name the layer.");
        TestAssert.Equal(0, plan.Layers.Count, "Invalid configured layers should not be added to layer plan.");
    }

    public static void AlternateBasemapRoleLayersAreAddedAsReferenceOptions()
    {
        var imageryLayer = new WorkingMapLayerPlan(
            "Esri World Imagery",
            "map_service",
            "https://example.test/imagery",
            "Basemaps",
            false,
            true,
            0,
            1.0,
            "imagery",
            null,
            null);
        var streetsLayer = imageryLayer with
        {
            Name = "Open Basemap Streets",
            Url = "https://example.test/streets/root.json",
            Visible = false,
            Order = 1,
            BasemapRole = "streets"
        };

        TestAssert.False(
            ArcGisWorkingMapPreparationService.ShouldAddReferenceLayer(imageryLayer, "esri_world_imagery"),
            "The active default imagery basemap should not be duplicated as an operational layer.");
        TestAssert.True(
            ArcGisWorkingMapPreparationService.ShouldAddReferenceLayer(streetsLayer, "esri_world_imagery"),
            "Open Basemap Streets should be added as a configured alternate/reference layer when imagery is the default basemap.");
    }

    public static void WorkingMapZoomUsesConfiguredExtentSpatialReference()
    {
        var source = File.ReadAllText(FindWorkingMapPreparationService());

        TestAssert.True(
            source.Contains("SpatialReferenceBuilder.CreateSpatialReference(extent.Wkid)", StringComparison.Ordinal),
            "Working map zoom should use the configured extent WKID, not a hard-coded spatial reference.");
        TestAssert.False(
            source.Contains("SpatialReferences.WGS84)", StringComparison.Ordinal),
            "Working map zoom must not treat JAD2001 meter extents as WGS84 degrees.");
    }

    public static void WorkingMapEnforcesJad2001MapSpatialReference()
    {
        var source = File.ReadAllText(FindWorkingMapPreparationService());

        TestAssert.True(
            source.Contains("private const int Jad2001Wkid = 3448", StringComparison.Ordinal),
            "Working map preparation must keep JAD2001/EPSG:3448 as the single workflow map spatial reference.");
        TestAssert.True(
            source.Contains("map.SetSpatialReference(SpatialReferenceBuilder.CreateSpatialReference(Jad2001Wkid))", StringComparison.Ordinal),
            "Working map preparation must explicitly set the ArcGIS Pro map spatial reference to JAD2001.");
        TestAssert.True(
            source.Contains("EnsureJad2001SpatialReferenceAsync(activeView.Map", StringComparison.Ordinal),
            "Reused active workflow maps must also be corrected to JAD2001.");
    }

    private static string FindWorkingMapPreparationService()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                "Workflow",
                "Maps",
                "IWorkingMapPreparationService.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate IWorkingMapPreparationService.cs from the test output directory.");
    }
}
