using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ParcelScopedManualPointServiceTests
{
    public static void CreateManualRowUsesActiveParcelContextAndNextSequence()
    {
        var document = new ExtractionReviewDocument();
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "row-1",
            ParcelGroupId = "114300701",
            ParcelName = "114300701",
            TraverseId = "114300701",
            SequenceInGroup = 12,
            PointIdentifier = "114300701_P12",
            Easting = "680668.236",
            Northing = "642731.568"
        });

        var service = new ParcelScopedManualPointService();
        var manualRow = service.CreateManualRow(document, "114300701", "114300701", "114300701");

        TestAssert.Equal("114300701", manualRow.ParcelGroupId, "Manual row should inherit the active parcel group.");
        TestAssert.Equal("114300701", manualRow.ParcelName, "Manual row should inherit the active parcel name.");
        TestAssert.Equal("114300701", manualRow.TraverseId, "Manual row should inherit the active traverse id.");
        TestAssert.Equal(13, manualRow.SequenceInGroup ?? -1, "Manual row should take the next parcel-local sequence.");
        TestAssert.Equal("114300701_P13", manualRow.PointIdentifier, "Manual row should initialize a parcel-local point id.");
        TestAssert.True(manualRow.IsManual, "Manual row should be flagged as manual.");
    }
}
