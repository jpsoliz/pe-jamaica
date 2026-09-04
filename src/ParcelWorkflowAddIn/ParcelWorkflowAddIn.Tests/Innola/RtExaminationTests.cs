using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.RtExamination;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class RtExaminationTests
{
    public static void SettingsDefaultsExposeRtExaminationStage()
    {
        var settings = InnolaTransactionSettings.Default.RtExamination;

        TestAssert.True(settings.Enabled, "RT Examination should be enabled by default.");
        TestAssert.Equal("In RT Examination", settings.StageName, "Default RT stage mismatch.");
        TestAssert.Equal("RT Examination", settings.SubworkflowName, "Default RT subworkflow mismatch.");
        TestAssert.Equal("PE_number", settings.WorkingReviewPeNumberField, "Default working_review PE field mismatch.");
    }

    public static void StageRouterRecognizesRtExaminationIndependentlyOfTransactionType()
    {
        var route = ParcelWorkflowStageRouter.Resolve(
            "In RT Examination",
            new[] { "Compute Survey Plan" },
            new[] { "Compare Survey Plan" },
            RtExaminationSettings.Default);

        TestAssert.Equal(ParcelWorkflowStageRoute.RtExamination, route, "In RT Examination should route to the RT workspace.");
    }

    public static void PartyRolesAreConstrainedForRtReview()
    {
        var roles = RtExaminationPartyRow.AllowedRoles;

        TestAssert.True(roles.Contains("Neighbor"), "Neighbor role missing.");
        TestAssert.True(roles.Contains("Owner"), "Owner role missing.");
        TestAssert.True(roles.Contains("Occupier"), "Occupier role missing.");
        TestAssert.True(roles.Contains("Representative"), "Representative role missing.");
        TestAssert.False(RtExaminationPartyRow.IsAllowedRole("Applicant"), "Unexpected Applicant role should not be allowed.");
    }

    public static void SpatialUnitEditableFieldsExcludeGeometry()
    {
        TestAssert.True(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("landValNumber"), "landValNumber should be editable.");
        TestAssert.True(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("examNumber"), "examNumber should be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("geometry"), "geometry must not be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("coordinates"), "coordinates must not be editable.");
        TestAssert.False(RtExaminationSpatialUnitFieldPolicy.IsEditableAttribute("bfsMinus"), "boundary fields must not be editable.");
    }


    public static void WindowXamlExposesReviewTabsEditableColumnsAndActions()
    {
        var source = File.ReadAllText(FindSourceFile("RtExaminationWindow.xaml"));

        foreach (var expected in new[]
        {
            "Header=\"Context\"",
            "Header=\"Neighbors / Parties\"",
            "Header=\"Spatial Units\"",
            "Header=\"Plan Check\"",
            "Header=\"Sources / Map Evidence\"",
            "LoadLinkedPeDataCommand",
            "SaveCommand",
            "CompleteCommand",
            "Header=\"Role\"",
            "Header=\"Address\"",
            "Header=\"LandVal No.\"",
            "Header=\"Exam No.\"",
            "Binding=\"{Binding ReviewedValue, Mode=TwoWay"
        })
        {
            TestAssert.True(source.Contains(expected, StringComparison.Ordinal), $"RT Examination window is missing expected surface: {expected}.");
        }

        TestAssert.True(
            source.Contains("ComboBox ItemsSource=\"{Binding AllowedRoles}\"", StringComparison.Ordinal),
            "RT role editing should bind the combo list from each editable party row.");
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
    }

    public static void DuplicatePartyRowsUseDeterministicRtKey()
    {
        var first = new RtExaminationPartyRow("Neighbor", "A Brown", "1 King St", "1158", "604", "7", "LV-1", "EX-1");
        var duplicate = new RtExaminationPartyRow("neighbor", " A Brown ", "1 King St", "1158", "604", "7", "LV-1", "EX-1");
        var different = first with { Folio = "605" };

        TestAssert.Equal(first.DeduplicationKey, duplicate.DeduplicationKey, "Equivalent RT party rows should have the same dedupe key.");
        TestAssert.True(!string.Equals(first.DeduplicationKey, different.DeduplicationKey, StringComparison.Ordinal), "Different RT party rows should not collapse.");
    }
}