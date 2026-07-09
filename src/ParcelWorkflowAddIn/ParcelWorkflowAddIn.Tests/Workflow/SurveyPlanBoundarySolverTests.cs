using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class SurveyPlanBoundarySolverTests
{
    public static void ParsesJamaicaQuadrantBearings()
    {
        var first = SurveyPlanBearingParser.ParseDelta("S84°56'E", "33.470");
        var second = SurveyPlanBearingParser.ParseDelta("N82 59 W", "41.415 m");

        TestAssert.True(first.Success, "S84°56'E should parse.");
        TestAssert.True(second.Success, "N82 59 W should parse.");
        TestAssert.True(first.DeltaEasting > 33d && first.DeltaNorthing < 0d, "S84°56'E should move mostly east and slightly south.");
        TestAssert.True(second.DeltaEasting < -41d && second.DeltaNorthing > 0d, "N82 59 W should move mostly west and slightly north.");
    }

    public static void SolvesTr100000562ReviewedBoundarySegments()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000562",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-15",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "15",
            Easting = "712897.345",
            Northing = "670582.156",
            ExtractionStatus = "printed_coordinate"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-17",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "17",
            Easting = "712856.553",
            Northing = "670563.653",
            ExtractionStatus = "printed_coordinate"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-16",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "16",
            Easting = "",
            Northing = "",
            ExtractionStatus = "missing_coordinate"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-18",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "18",
            Easting = "",
            Northing = "",
            ExtractionStatus = "missing_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "18", ToPoint = "15", BearingText = "S84°56'E", DistanceText = "33.470" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "15", ToPoint = "30", BearingText = "S01°27'E", DistanceText = "18.343" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "30", ToPoint = "16", BearingText = "S01°39'W", DistanceText = "5.230" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "16", ToPoint = "17", BearingText = "N82°59'W", DistanceText = "41.415" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-5", Sequence = 5, FromPoint = "17", ToPoint = "18", BearingText = "N19°09'E", DistanceText = "22.715" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, 854.807);

        TestAssert.True(result.DerivedPointCount >= 3, "Solver should derive missing points from reviewed segments.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "18" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Point 18 should be derived.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "30" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Point 30 should be derived.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "16" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Point 16 should be derived.");
        TestAssert.True(!string.IsNullOrWhiteSpace(document.Rows.First(row => row.PointIdentifier == "16").Easting), "Solver should fill existing blank point 16 easting.");
        TestAssert.True(!string.IsNullOrWhiteSpace(document.Rows.First(row => row.PointIdentifier == "18").Northing), "Solver should fill existing blank point 18 northing.");
        TestAssert.True(result.ComputedAreaSqM.HasValue, "Solver should compute polygon area.");
        TestAssert.True(Math.Abs(result.ComputedAreaSqM.Value - 854.807) < 2.0, "Computed area should be close to the document area.");
        TestAssert.True(result.ClosureDistanceM.GetValueOrDefault(999d) < 1.0, "Solved boundary should close within one metre.");
        TestAssert.True(document.RootMetadata.ContainsKey("boundary_solver"), "Solver diagnostics should be written into review metadata.");
    }

    public static void DetectsPrintedCoordinateConflicts()
    {
        var document = new ExtractionReviewDocument { TransactionNumber = "100000562" };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-15",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "15",
            Easting = "712897.345",
            Northing = "670582.156",
            ExtractionStatus = "printed_coordinate"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-30",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "30",
            Easting = "1.0",
            Northing = "1.0",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "15", ToPoint = "30", BearingText = "S01°27'E", DistanceText = "18.343" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, null);

        TestAssert.True(result.Findings.Any(finding => finding.Contains("conflict", StringComparison.OrdinalIgnoreCase)), "Conflicting printed coordinates should be reported.");
        TestAssert.Equal("1.0", document.Rows.First(row => row.PointIdentifier == "30").Easting, "Solver must not overwrite printed coordinates silently.");
    }

    public static void BlocksIncompleteReviewedBoundaryChains()
    {
        var document = new ExtractionReviewDocument { TransactionNumber = "100000562" };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-15",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "15",
            Easting = "712897.345",
            Northing = "670582.156",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "15", ToPoint = "", BearingText = "S01°27'E", DistanceText = "18.343" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, 854.807);

        TestAssert.Equal("blocked", result.Status, "Incomplete reviewed segment chains should block validation completion.");
        TestAssert.True(result.Findings.Any(finding => finding.Contains("missing", StringComparison.OrdinalIgnoreCase)), "Blocker should explain the missing segment endpoint.");
    }
}
