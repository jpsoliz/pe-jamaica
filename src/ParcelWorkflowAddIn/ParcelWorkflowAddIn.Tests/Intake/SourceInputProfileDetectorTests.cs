using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Tests.Intake;

internal static class SourceInputProfileDetectorTests
{
    public static void DetectsScenarioAFromComputationAndPlanRoles()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("computation.pdf", ".pdf", "computation_source"),
            Source("plan.tif", ".tif", "plan_map_reference")
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("scenario_a", profile.ProfileCode, "Scenario A profile code mismatch.");
        TestAssert.True(profile.DisplayLabel.Contains("Scenario A", StringComparison.Ordinal), "Scenario A label expected.");
        TestAssert.Equal("matched", profile.Status, "Scenario A should be matched.");
        TestAssert.Equal(0, profile.MissingRoles.Count, "Scenario A should not have missing roles.");
    }

    public static void DetectsScenarioBFromPointsDwgAndPlanRoles()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("computation.pdf", ".pdf", "computation_source"),
            Source("points.csv", ".csv", "points_computation"),
            Source("reference.dwg", ".dwg", "dwg_reference"),
            Source("plan.pdf", ".pdf", "plan_map_reference")
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("scenario_b", profile.ProfileCode, "Scenario B profile code mismatch.");
        TestAssert.True(profile.DisplayLabel.Contains("Scenario B", StringComparison.Ordinal), "Scenario B label expected.");
        TestAssert.Equal("matched", profile.Status, "Scenario B should be matched.");
        TestAssert.Equal(0, profile.MissingRoles.Count, "Scenario B should not have missing roles.");
    }

    public static void DetectsIncompleteIntakeWithMissingRoles()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("points.csv", ".csv", null)
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("incomplete_intake", profile.ProfileCode, "Incomplete profile code mismatch.");
        TestAssert.Equal("incomplete", profile.Status, "Incomplete status mismatch.");
        TestAssert.True(profile.MissingRoles.Contains("plan_map_reference"), "Plan/map reference should be reported missing.");
        TestAssert.True(profile.Issues.Count > 0, "Incomplete intake should include issues.");
    }

    public static void InfersTxtCsvAsPointsComputationWhenComputationAndPlanExist()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("computation.pdf", ".pdf", "computation_source"),
            Source("survey.csv", ".csv", null),
            Source("reference.dwg", ".dwg", null),
            Source("plan.pdf", ".pdf", "plan_map_reference")
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("scenario_b", profile.ProfileCode, "CSV should infer points/computation without filename hints.");
        TestAssert.Equal("matched", profile.Status, "Computation/CSV/DWG/plan should match Scenario B.");
    }

    public static void InfersLiveTwoPdfComputationAndPlanFilenames()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("BELLEV029GEOLANCOMSHEET.pdf", ".pdf", null),
            Source("BELLEV029GEOLAN20230811.pdf", ".pdf", null)
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("scenario_a", profile.ProfileCode, "Live two-PDF transaction should match Scenario A.");
        TestAssert.Equal("matched", profile.Status, "Live two-PDF transaction should not remain incomplete.");
        TestAssert.Equal(0, profile.MissingRoles.Count, "Live two-PDF transaction should not have missing roles.");
    }

    public static void DetectsUnsupportedIntakeWithoutClaimingScenario()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("field_notes.zip", ".zip", null)
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("unsupported_intake", profile.ProfileCode, "Unsupported profile code mismatch.");
        TestAssert.Equal("unsupported", profile.Status, "Unsupported status mismatch.");
        TestAssert.True(!profile.DisplayLabel.Contains("Scenario A", StringComparison.Ordinal), "Unsupported intake should not claim Scenario A.");
        TestAssert.True(!profile.DisplayLabel.Contains("Scenario B", StringComparison.Ordinal), "Unsupported intake should not claim Scenario B.");
    }

    public static void DwgOnlyIntakeIsIncompleteWithScenarioBMissingRoles()
    {
        var detector = new SourceInputProfileDetector(() => new DateTimeOffset(2026, 6, 9, 2, 0, 0, TimeSpan.Zero));
        var sources = new[]
        {
            Source("dwg_only.dwg", ".dwg", "dwg_reference")
        };

        var profile = detector.Detect(sources);

        TestAssert.Equal("incomplete_intake", profile.ProfileCode, "DWG-only intake should be incomplete.");
        TestAssert.Equal("incomplete", profile.Status, "DWG-only status should be incomplete.");
        TestAssert.True(profile.MissingRoles.Contains("computation_sheet"), "DWG-only intake should report missing computation sheet.");
        TestAssert.True(profile.MissingRoles.Contains("plan_map_reference"), "DWG-only intake should report missing plan/map reference.");
    }

    public static void ProductionLabelsNeverExposeFixtureCaseLabels()
    {
        var detector = new SourceInputProfileDetector(() => DateTimeOffset.UtcNow);
        var profiles = new[]
        {
            detector.Detect(new[] { Source("computation.pdf", ".pdf", "computation_source"), Source("plan.pdf", ".pdf", "plan_map_reference") }),
            detector.Detect(new[] { Source("points.csv", ".csv", "points_computation"), Source("reference.dwg", ".dwg", "dwg_reference"), Source("plan.pdf", ".pdf", "plan_map_reference") }),
            detector.Detect(new[] { Source("points.csv", ".csv", null) }),
            detector.Detect(new[] { Source("dwg_only.dwg", ".dwg", "dwg_reference") })
        };

        foreach (var profile in profiles)
        {
            foreach (var forbidden in new[] { "Case 1", "Case 2", "Case 3", "Case 4" })
            {
                TestAssert.True(!profile.DisplayLabel.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Production label must not expose {forbidden}.");
            }
        }
    }

    private static ManifestSourceFile Source(string fileName, string fileType, string? sourceRole)
    {
        return new ManifestSourceFile(
            Path.Combine("C:\\incoming", fileName),
            Path.Combine("D:\\cases\\TR-SMD-0000001\\source", fileName),
            fileType,
            100,
            "2026-06-09T00:00:00Z",
            sourceRole);
    }
}
