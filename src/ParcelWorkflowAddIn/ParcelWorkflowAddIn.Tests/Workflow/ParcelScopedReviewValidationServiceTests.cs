using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ParcelScopedReviewValidationServiceTests
{
    public static void ValidateBlocksDuplicatePointIdsWithinParcel()
    {
        var service = new ParcelScopedReviewValidationService();
        var rows = new[]
        {
            new ExtractionReviewRow
            {
                RowId = "row-1",
                ParcelGroupId = "parcel-001",
                SequenceInGroup = 1,
                PointIdentifier = "P-101",
                Easting = "680681.247",
                Northing = "642710.038"
            },
            new ExtractionReviewRow
            {
                RowId = "row-2",
                ParcelGroupId = "parcel-001",
                SequenceInGroup = 2,
                PointIdentifier = "P-101",
                Easting = "680692.719",
                Northing = "642690.087"
            }
        };

        var result = service.Validate(rows);

        TestAssert.True(result.HasBlockers, "Duplicate point ids inside one parcel should block approval.");
        TestAssert.True(result.Issues.Any(issue => issue.Contains("duplicate point id", StringComparison.OrdinalIgnoreCase)), "Duplicate point id issue should be reported.");
    }

    public static void ValidateBlocksPendingManualEditAndInvalidCoordinates()
    {
        var service = new ParcelScopedReviewValidationService();
        var rows = new[]
        {
            new ExtractionReviewRow
            {
                RowId = "manual-parcel-001-003",
                ParcelGroupId = "parcel-001",
                SequenceInGroup = 3,
                PointIdentifier = "parcel-001_P3",
                Easting = "bad-easting",
                Northing = "642690.087",
                IsManual = true
            }
        };

        var result = service.Validate(rows, "manual-parcel-001-003");

        TestAssert.True(result.HasBlockers, "Pending manual edit should block approval.");
        TestAssert.True(result.Issues.Any(issue => issue.Contains("invalid numeric coordinates", StringComparison.OrdinalIgnoreCase)), "Invalid coordinates should be reported.");
        TestAssert.True(result.Issues.Any(issue => issue.Contains("Save or discard the in-progress manual point", StringComparison.OrdinalIgnoreCase)), "Pending manual edit should be reported.");
    }

    public static void ValidateBlocksManualRowsWithoutParcelAssignmentOrValidSequence()
    {
        var service = new ParcelScopedReviewValidationService();
        var rows = new[]
        {
            new ExtractionReviewRow
            {
                RowId = "manual-001",
                ParcelGroupId = string.Empty,
                SequenceInGroup = 0,
                PointIdentifier = "P-1",
                Easting = "680681.247",
                Northing = "642710.038",
                IsManual = true
            }
        };

        var result = service.Validate(rows);

        TestAssert.True(result.HasBlockers, "Manual rows without parcel assignment or valid sequence should block approval.");
        TestAssert.True(result.Issues.Any(issue => issue.Contains("missing parcel assignment", StringComparison.OrdinalIgnoreCase)), "Missing parcel assignment should be reported.");
        TestAssert.True(result.Issues.Any(issue => issue.Contains("missing or invalid sequence", StringComparison.OrdinalIgnoreCase)), "Invalid sequence should be reported.");
    }

    public static void ValidateBlocksReadinessGapInParcelSequence()
    {
        var service = new ParcelScopedReviewValidationService();
        var rows = new[]
        {
            new ExtractionReviewRow
            {
                RowId = "row-1",
                ParcelGroupId = "parcel-001",
                SequenceInGroup = 1,
                PointIdentifier = "P-1",
                Easting = "680681.247",
                Northing = "642710.038"
            },
            new ExtractionReviewRow
            {
                RowId = "row-2",
                ParcelGroupId = "parcel-001",
                SequenceInGroup = 3,
                PointIdentifier = "P-2",
                Easting = "680692.719",
                Northing = "642690.087"
            },
            new ExtractionReviewRow
            {
                RowId = "row-3",
                ParcelGroupId = "parcel-001",
                SequenceInGroup = 4,
                PointIdentifier = "P-3",
                Easting = "680654.865",
                Northing = "642664.265"
            }
        };

        var result = service.Validate(rows);

        TestAssert.True(result.HasBlockers, "Missing parcel sequence values should block approval.");
        TestAssert.True(
            result.ReadinessResults.Any(issue =>
                issue.Category.Contains("boundary_completeness", StringComparison.OrdinalIgnoreCase)
                && issue.Status == ReadinessValidationStatus.Blocker),
            "Boundary completeness readiness blocker should be reported.");
    }
}
