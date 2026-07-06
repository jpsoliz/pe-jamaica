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

    public static void CreateManualRowInsertsAfterSelectedSequenceAndShiftsLaterRows()
    {
        var document = new ExtractionReviewDocument();
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "row-12",
            ParcelGroupId = "110900205",
            ParcelName = "110900205",
            TraverseId = "110900205",
            SequenceInGroup = 13,
            PointIdentifier = "110900205_P12",
            Easting = "669773.4511",
            Northing = "644377.2902"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "row-14",
            ParcelGroupId = "110900205",
            ParcelName = "110900205",
            TraverseId = "110900205",
            SequenceInGroup = 14,
            PointIdentifier = "110900205_P14",
            Easting = "669654.9159",
            Northing = "644350.3226"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "other-row",
            ParcelGroupId = "parcel-007",
            ParcelName = "parcel-007",
            TraverseId = "parcel-007",
            SequenceInGroup = 14,
            PointIdentifier = "parcel-007_P14",
            Easting = "1",
            Northing = "1"
        });

        var service = new ParcelScopedManualPointService();
        var manualRow = service.CreateManualRow(
            document,
            "110900205",
            "110900205",
            "110900205",
            insertAfterSequence: 13,
            insertAfterPointIdentifier: "110900205_P12");

        TestAssert.Equal(14, manualRow.SequenceInGroup ?? -1, "Inserted manual row should take the sequence immediately after the selected row.");
        TestAssert.Equal("110900205_P13", manualRow.PointIdentifier, "Inserted manual row should default to the next point identifier after the selected point.");
        TestAssert.Equal(15, document.Rows.First(row => row.RowId == "row-14").SequenceInGroup ?? -1, "Later rows in the same parcel should shift forward.");
        TestAssert.Equal(14, document.Rows.First(row => row.RowId == "other-row").SequenceInGroup ?? -1, "Rows in other parcels must not be shifted.");
    }
}
