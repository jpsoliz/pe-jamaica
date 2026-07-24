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
        TestAssert.Equal(1, document.Rows.First(row => row.PointIdentifier == "18").SequenceInGroup, "Point 18 should start the reviewed boundary sequence.");
        TestAssert.Equal(2, document.Rows.First(row => row.PointIdentifier == "15").SequenceInGroup, "Point 15 should follow point 18 in the reviewed boundary sequence.");
        TestAssert.Equal(3, document.Rows.First(row => row.PointIdentifier == "30").SequenceInGroup, "Point 30 should follow point 15 in the reviewed boundary sequence.");
        TestAssert.Equal(4, document.Rows.First(row => row.PointIdentifier == "16").SequenceInGroup, "Point 16 should follow point 30 in the reviewed boundary sequence.");
        TestAssert.Equal(5, document.Rows.First(row => row.PointIdentifier == "17").SequenceInGroup, "Point 17 should close the reviewed boundary sequence before the implicit return to point 18.");
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

        TestAssert.True(result.Findings.Any(finding => finding.Contains("existing coordinates that do not match the reviewed boundary path", StringComparison.OrdinalIgnoreCase)), "Conflicting printed coordinates should be reported with actionable wording.");
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

    public static void RecalculatesPreviouslyDerivedRowsWhenSegmentsChange()
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
            Easting = "1.0",
            Northing = "1.0",
            ExtractionStatus = "derived_from_reviewed_segments",
            RowProvenance = "derived_from_reviewed_segments"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "18", ToPoint = "15", BearingText = "S84°56'E", DistanceText = "33.470" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "15", ToPoint = "30", BearingText = "S01°27'E", DistanceText = "18.343" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "30", ToPoint = "16", BearingText = "S01°39'W", DistanceText = "5.230" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "16", ToPoint = "17", BearingText = "N82°59'W", DistanceText = "41.415" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-5", Sequence = 5, FromPoint = "17", ToPoint = "18", BearingText = "N19°09'E", DistanceText = "22.715" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, 854.807);

        TestAssert.True(!result.Findings.Any(finding => finding.Contains("conflict", StringComparison.OrdinalIgnoreCase)), "Previously derived rows should be recalculated instead of treated as printed-coordinate conflicts.");
        TestAssert.True(double.Parse(document.Rows.First(row => row.PointIdentifier == "16").Easting, System.Globalization.CultureInfo.InvariantCulture) > 700000d, "Point 16 should be recalculated from the reviewed boundary segments.");
    }

    public static void RebuildCanUseDerivedRowsAsAnchorsForNewSegments()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "1000",
            Northing = "1000",
            ExtractionStatus = "printed_coordinate"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-b",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "B",
            Easting = "1030",
            Northing = "1000",
            ExtractionStatus = "derived_from_reviewed_segments",
            RowProvenance = "derived_from_reviewed_segments"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "B", BearingText = "N90°00'E", DistanceText = "30" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "B", ToPoint = "S", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "S", ToPoint = "T", BearingText = "N90°00'W", DistanceText = "20" });

        var solver = new SurveyPlanBoundarySolver();
        solver.Apply(document, null, useDerivedCoordinatesAsAnchors: true);

        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "S" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Explicit rebuild should derive new point S from existing derived point B.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "T" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Explicit rebuild should continue deriving downstream points from the rebuilt chain.");
    }

    public static void RebuildReplacesConflictingExistingCoordinatesFromReviewedSegments()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000855",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate"
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-b",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "B",
            Easting = "99",
            Northing = "99",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "B", BearingText = "N90°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "B", ToPoint = "C", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "C", ToPoint = "D", BearingText = "N90°00'W", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "D", ToPoint = "A", BearingText = "N00°00'E", DistanceText = "10" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(
            document,
            null,
            useDerivedCoordinatesAsAnchors: true,
            repairPrematureClosingLabels: true,
            replaceConflictingCoordinatesFromReviewedSegments: true);

        var pointB = document.Rows.First(row => row.PointIdentifier == "B");
        TestAssert.Equal("10", pointB.Easting, "Explicit rebuild should replace conflicting existing coordinates from reviewed segments.");
        TestAssert.Equal("0", pointB.Northing, "Explicit rebuild should replace conflicting existing coordinates from reviewed segments.");
        TestAssert.True(result.Findings.Any(finding => finding.Contains("was recalculated", StringComparison.OrdinalIgnoreCase)), "Rebuild should explain that a conflicting point was recalculated.");
        TestAssert.True(result.ClosureDistanceM.GetValueOrDefault(999d) < 0.01, "Rebuilt geometry should close.");
    }

    public static void RebuildRepairsPrematureAlphabeticClosingLabelReuse()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "B", BearingText = "N90°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "B", ToPoint = "A", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "A", ToPoint = "D", BearingText = "N90°00'W", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "D", ToPoint = "A", BearingText = "N00°00'E", DistanceText = "10" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, null, useDerivedCoordinatesAsAnchors: true, repairPrematureClosingLabels: true);

        TestAssert.True(result.Findings.Any(finding => finding.Contains("repeated point labels", StringComparison.OrdinalIgnoreCase)), "Rebuild should explain the automatic label repair.");
        TestAssert.Equal("1", document.Segments[1].EffectiveToPoint, "Lettered plan labels should generate numeric intermediate labels.");
        TestAssert.Equal("1", document.Segments[2].EffectiveFromPoint, "The following segment should continue from the generated numeric label.");
        TestAssert.Equal("A", document.Segments[^1].EffectiveToPoint, "The final closure label should be preserved.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "1" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Generated numeric point 1 should be derived from the repaired chain.");
        TestAssert.True(result.ClosureDistanceM.GetValueOrDefault(999d) < 0.01, "Repaired alphabetic chain should close.");
    }

    public static void RebuildRepairsPrematureNumericClosingLabelReuse()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-1",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "1",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "1", ToPoint = "2", BearingText = "N90°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "2", ToPoint = "1", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "1", ToPoint = "3", BearingText = "N90°00'W", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "3", ToPoint = "1", BearingText = "N00°00'E", DistanceText = "10" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, null, useDerivedCoordinatesAsAnchors: true, repairPrematureClosingLabels: true);

        TestAssert.Equal("A", document.Segments[1].EffectiveToPoint, "Numbered plan labels should generate alphabetic intermediate labels.");
        TestAssert.Equal("A", document.Segments[2].EffectiveFromPoint, "The following segment should continue from the generated alphabetic label.");
        TestAssert.Equal("1", document.Segments[^1].EffectiveToPoint, "The final numeric closure label should be preserved.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "A" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Generated alphabetic point A should be derived from the repaired chain.");
        TestAssert.True(result.ClosureDistanceM.GetValueOrDefault(999d) < 0.01, "Repaired numeric chain should close.");
    }

    public static void RebuildRepairsRepeatedLabelsAfterEarlyClosure()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "B", BearingText = "N55°17'W", DistanceText = "9.718" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "B", ToPoint = "C", BearingText = "N60°36'W", DistanceText = "20.599" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "C", ToPoint = "D", BearingText = "N70°36'W", DistanceText = "7.589" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "D", ToPoint = "E", BearingText = "N79°50'W", DistanceText = "20.984" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-5", Sequence = 5, FromPoint = "E", ToPoint = "F", BearingText = "N87°55'W", DistanceText = "10.921" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-6", Sequence = 6, FromPoint = "F", ToPoint = "G", BearingText = "N71°19'W", DistanceText = "21.872" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-7", Sequence = 7, FromPoint = "G", ToPoint = "H", BearingText = "N37°46'E", DistanceText = "100.330" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-8", Sequence = 8, FromPoint = "H", ToPoint = "A", BearingText = "S56°29'E", DistanceText = "14.815" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-9", Sequence = 9, FromPoint = "A", ToPoint = "B", BearingText = "S82°44'E", DistanceText = "26.670" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-10", Sequence = 10, FromPoint = "B", ToPoint = "A", BearingText = "N73°01'E", DistanceText = "12.283" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-11", Sequence = 11, FromPoint = "A", ToPoint = "B", BearingText = "S13°47'W", DistanceText = "73.736" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-12", Sequence = 12, FromPoint = "B", ToPoint = "A", BearingText = "S17°20'W", DistanceText = "30.387" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, null, useDerivedCoordinatesAsAnchors: true, repairPrematureClosingLabels: true);

        var repairedPath = string.Join(",", document.Segments.Select(segment => $"{segment.EffectiveFromPoint}->{segment.EffectiveToPoint}"));
        TestAssert.Equal("A->B,B->C,C->D,D->E,E->F,F->G,G->H,H->1,1->2,2->3,3->4,4->A", repairedPath, "Rebuild should convert repeated letter labels into numeric intermediate labels while preserving the final A closure.");
        TestAssert.True(result.Findings.Any(finding => finding.Contains("1, 2, 3, 4", StringComparison.OrdinalIgnoreCase)), "Rebuild findings should list all generated labels.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "1" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Generated point 1 should be derived.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "4" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Generated point 4 should be derived.");
        TestAssert.True(result.ClosureDistanceM.GetValueOrDefault(999d) < 0.01, "The repaired TR100000854-style chain should close from bearings and distances.");
    }

    public static void RebuildRepairsRepeatedLabelsWhenFinalClosureLabelIsWrong()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate"
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "B", BearingText = "N55°17'W", DistanceText = "9.718" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "B", ToPoint = "C", BearingText = "N60°36'W", DistanceText = "20.599" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "C", ToPoint = "D", BearingText = "N70°36'W", DistanceText = "7.589" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "D", ToPoint = "E", BearingText = "N79°50'W", DistanceText = "20.984" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-5", Sequence = 5, FromPoint = "E", ToPoint = "F", BearingText = "N87°55'W", DistanceText = "10.921" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-6", Sequence = 6, FromPoint = "F", ToPoint = "G", BearingText = "N71°19'W", DistanceText = "21.872" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-7", Sequence = 7, FromPoint = "G", ToPoint = "A", BearingText = "N37°46'E", DistanceText = "100.330" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-8", Sequence = 8, FromPoint = "A", ToPoint = "B", BearingText = "S56°29'E", DistanceText = "14.815" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-9", Sequence = 9, FromPoint = "B", ToPoint = "C", BearingText = "S82°44'E", DistanceText = "26.670" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-10", Sequence = 10, FromPoint = "C", ToPoint = "D", BearingText = "N73°01'E", DistanceText = "12.283" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-11", Sequence = 11, FromPoint = "D", ToPoint = "E", BearingText = "S13°47'W", DistanceText = "73.736" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-12", Sequence = 12, FromPoint = "E", ToPoint = "F", BearingText = "S17°20'W", DistanceText = "30.387" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(document, null, useDerivedCoordinatesAsAnchors: true, repairPrematureClosingLabels: true);

        var repairedPath = string.Join(",", document.Segments.Select(segment => $"{segment.EffectiveFromPoint}->{segment.EffectiveToPoint}"));
        TestAssert.Equal("A->B,B->C,C->D,D->E,E->F,F->G,G->1,1->2,2->3,3->4,4->5,5->A", repairedPath, "Rebuild should use geometric closure to repair wrong repeated labels with numeric generated labels.");
        TestAssert.True(result.ClosureDistanceM.GetValueOrDefault(999d) < 0.01, "The repaired chain should close after replacing the wrong final label with A.");
    }

    public static void RebuildRemovesStaleDerivedRowsNoLongerReferencedByBoundary()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 1
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-stale",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "7",
            Easting = "5",
            Northing = "5",
            ExtractionStatus = "derived_from_reviewed_segments",
            RowProvenance = "derived_from_reviewed_segments",
            SequenceInGroup = 3
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "1", BearingText = "N90°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "1", ToPoint = "B", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "B", ToPoint = "2", BearingText = "N90°00'W", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "2", ToPoint = "A", BearingText = "N00°00'E", DistanceText = "10" });

        var solver = new SurveyPlanBoundarySolver();
        solver.Apply(document, null, useDerivedCoordinatesAsAnchors: true, repairPrematureClosingLabels: true);

        TestAssert.False(document.Rows.Any(row => row.PointIdentifier == "7"), "Rebuild should remove stale solver-derived point rows that are no longer in the reviewed boundary chain.");
        var sequences = document.Rows
            .Where(row => string.Equals(row.ParcelGroupId, "parcel-001", StringComparison.OrdinalIgnoreCase))
            .Select(row => row.SequenceInGroup ?? 0)
            .ToArray();
        TestAssert.Equal(sequences.Length, sequences.Distinct().Count(), "Rebuild should leave the parcel point sequence without duplicates.");
        TestAssert.Equal(4, sequences.Length, "Only the four active boundary points should remain.");
    }

    public static void RebuildRemovesInactiveExtractedRowsNoLongerReferencedByBoundary()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 1
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-b-reference",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "B",
            Easting = "100",
            Northing = "100",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 2
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "1", BearingText = "N90°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "1", ToPoint = "2", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "2", ToPoint = "A", BearingText = "N45°00'W", DistanceText = "14.142" });

        var solver = new SurveyPlanBoundarySolver();
        solver.Apply(
            document,
            null,
            useDerivedCoordinatesAsAnchors: true,
            repairPrematureClosingLabels: true,
            replaceConflictingCoordinatesFromReviewedSegments: true);

        TestAssert.False(document.Rows.Any(row => row.PointIdentifier == "B"), "Explicit rebuild should remove extracted/reference rows that are not part of the reviewed boundary chain.");
        var sequences = document.Rows.Select(row => row.SequenceInGroup ?? 0).ToArray();
        TestAssert.Equal(sequences.Length, sequences.Distinct().Count(), "Explicit rebuild should not leave duplicate sequence values from inactive extracted rows.");
        TestAssert.Equal(3, document.Rows.Count, "Only active reviewed boundary points should remain after explicit rebuild.");
    }

    public static void RebuildMergesGeneratedPointThatMatchesExtractedReferenceCoordinate()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000854",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-a",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "A",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 1
        });
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-b-reference",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "B",
            Easting = "0.002",
            Northing = "-10.001",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 2
        });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-1", Sequence = 1, FromPoint = "A", ToPoint = "1", BearingText = "N90°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-2", Sequence = 2, FromPoint = "1", ToPoint = "2", BearingText = "S00°00'E", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-3", Sequence = 3, FromPoint = "2", ToPoint = "7", BearingText = "N90°00'W", DistanceText = "10" });
        document.Segments.Add(new ExtractionReviewSegment { SegmentId = "seg-4", Sequence = 4, FromPoint = "7", ToPoint = "A", BearingText = "N00°00'E", DistanceText = "10" });

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(
            document,
            null,
            useDerivedCoordinatesAsAnchors: true,
            repairPrematureClosingLabels: true,
            replaceConflictingCoordinatesFromReviewedSegments: true);

        var repairedPath = string.Join(",", document.Segments.Select(segment => $"{segment.EffectiveFromPoint}->{segment.EffectiveToPoint}"));
        TestAssert.Equal("A->1,1->2,2->B,B->A", repairedPath, "Rebuild should replace generated point 7 with matching extracted reference point B.");
        TestAssert.False(document.Rows.Any(row => row.PointIdentifier == "7"), "Generated point 7 should not remain as an additional point row when it matches reference point B.");
        TestAssert.Equal(4, document.Rows.Count, "Closed parcel should have the same active point row count as segment count.");
        TestAssert.Equal(4, document.Rows.First(row => row.PointIdentifier == "B").SequenceInGroup ?? -1, "Reference point B should keep its row and receive its boundary sequence position.");
        TestAssert.True(result.Findings.Any(finding => finding.Contains("matched generated point 7", StringComparison.OrdinalIgnoreCase)), "Solver should explain the generated/reference point merge.");
    }

    public static void ExplicitRebuildReplacesStaleManualPointListWithReviewedBoundaryChain()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000872",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-5",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "5",
            Easting = "0",
            Northing = "0",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 1
        });
        document.Rows.Add(new ExtractionReviewRow { RowId = "point-2", ParcelGroupId = "parcel-001", PointIdentifier = "2", Easting = "100", Northing = "100", ExtractionStatus = "manual", IsManual = true, SequenceInGroup = 7 });
        document.Rows.Add(new ExtractionReviewRow { RowId = "point-f", ParcelGroupId = "parcel-001", PointIdentifier = "F", Easting = "110", Northing = "110", ExtractionStatus = "manual", IsManual = true, SequenceInGroup = 3 });
        document.Rows.Add(new ExtractionReviewRow { RowId = "point-is", ParcelGroupId = "parcel-001", PointIdentifier = "IS", Easting = "120", Northing = "120", ExtractionStatus = "manual", IsManual = true, SequenceInGroup = 5 });
        document.Rows.Add(new ExtractionReviewRow { RowId = "point-lk", ParcelGroupId = "parcel-001", PointIdentifier = "Lk.", Easting = "130", Northing = "130", ExtractionStatus = "manual", IsManual = true, SequenceInGroup = 5 });

        AddSegment(document, 1, "5", "6", "N0°00'E", "10");
        AddSegment(document, 2, "6", "7", "N90°00'E", "10");
        AddSegment(document, 3, "7", "8", "S0°00'E", "10");
        AddSegment(document, 4, "8", "5", "N90°00'W", "10");

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(
            document,
            null,
            useDerivedCoordinatesAsAnchors: true,
            repairPrematureClosingLabels: true,
            replaceConflictingCoordinatesFromReviewedSegments: true,
            mergeGeneratedBoundaryPointsWithReferenceRows: false,
            removeInactiveManualRows: true);

        TestAssert.True(!string.Equals(result.Status, "blocked", StringComparison.OrdinalIgnoreCase), $"Explicit rebuild should solve the reviewed chain. Findings: {string.Join(" | ", result.Findings)}");
        var pointIds = document.Rows
            .OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
            .Select(row => row.PointIdentifier)
            .ToArray();
        TestAssert.Equal("5,6,7,8", string.Join(",", pointIds), "Explicit rebuild should replace stale manual points with one row per reviewed boundary segment.");
        TestAssert.Equal(4, document.Rows.Count, "Explicit rebuild should leave the same active point row count as the boundary segment count.");
        TestAssert.False(document.Rows.Any(row => row.PointIdentifier is "2" or "F" or "IS" or "Lk."), "Stale manual/reference points outside the reviewed boundary chain should be removed.");
    }

    public static void RebuildGeneratesRowsForTr100000857ReviewedSegmentChain()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100000857",
            ExtractionSource = "survey_plan_ocr_vision"
        };
        document.Rows.Add(new ExtractionReviewRow
        {
            RowId = "point-1095",
            ParcelGroupId = "survey-plan-parcel",
            ParcelName = "survey-plan-parcel",
            TraverseId = "survey-plan-parcel",
            PointIdentifier = "1095",
            Easting = "680884.109",
            Northing = "657837.042",
            ExtractionStatus = "printed_coordinate",
            SequenceInGroup = 1
        });
        document.Rows.Add(new ExtractionReviewRow { RowId = "point-1084", ParcelGroupId = "survey-plan-parcel", PointIdentifier = "1084", Easting = "680933.933", Northing = "657910.501", ExtractionStatus = "printed_coordinate", SequenceInGroup = 1 });

        AddSegment(document, 1, "1095", "A", "N28°57'37\"W", "13.859");
        AddSegment(document, 2, "A", "1", "N50°21'50\"W", "33.628");
        AddSegment(document, 3, "1", "2", "N32°57'58\"E", "58.623");
        AddSegment(document, 4, "2", "3", "N22°09'08\"E", "1.758");
        AddSegment(document, 5, "3", "F", "S45°22'40\"E", "18.954");
        AddSegment(document, 6, "F", "4", "N86°38'19\"E", "24.928");
        AddSegment(document, 7, "4", "5", "S55°02'13\"E", "14.516");
        AddSegment(document, 8, "5", "6", "S28°34'31\"E", "10.080");
        AddSegment(document, 9, "6", "7", "S79°12'51\"E", "19.459");
        AddSegment(document, 10, "7", "8", "S20°16'53\"W", "2.028");
        AddSegment(document, 11, "8", "9", "S18°00'17\"E", "21.476");
        AddSegment(document, 12, "9", "10", "S19°53'32\"E", "9.325");
        AddSegment(document, 13, "10", "B", "S21°40'33\"E", "21.177");
        AddSegment(document, 14, "B", "11", "S16°33'38\"W", "23.640");
        AddSegment(document, 15, "11", "12", "S75°56'48\"W", "27.868");
        AddSegment(document, 16, "12", "13", "N6°19'40\"W", "18.500");
        AddSegment(document, 17, "13", "14", "S76°41'01\"W", "21.817");
        AddSegment(document, 18, "14", "15", "N0°22'31\"W", "24.578");
        AddSegment(document, 19, "15", "1095", "S74°23'50\"W", "35.187");

        var solver = new SurveyPlanBoundarySolver();
        var result = solver.Apply(
            document,
            null,
            useDerivedCoordinatesAsAnchors: true,
            repairPrematureClosingLabels: true,
            replaceConflictingCoordinatesFromReviewedSegments: true);

        TestAssert.True(!string.Equals(result.Status, "blocked", StringComparison.OrdinalIgnoreCase), $"TR100000857 chain should not block row generation. Findings: {string.Join(" | ", result.Findings)}");
        TestAssert.Equal(19, document.Rows.Count(row => row.ParcelGroupId == "survey-plan-parcel"), "Rebuild should produce one active point row for each boundary segment.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "A" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Point A should be generated from reviewed segments.");
        TestAssert.True(document.Rows.Any(row => row.PointIdentifier == "15" && row.ExtractionStatus == "derived_from_reviewed_segments"), "Point 15 should be generated from reviewed segments.");
        TestAssert.False(document.Rows.Any(row => row.PointIdentifier == "1084"), "Inactive reference point 1084 should be removed by explicit rebuild.");
    }

    private static void AddSegment(ExtractionReviewDocument document, int sequence, string fromPoint, string toPoint, string bearing, string distance)
    {
        document.Segments.Add(new ExtractionReviewSegment
        {
            SegmentId = $"seg-{sequence}",
            Sequence = sequence,
            FromPoint = fromPoint,
            ToPoint = toPoint,
            BearingText = bearing,
            DistanceText = distance
        });
    }
}
