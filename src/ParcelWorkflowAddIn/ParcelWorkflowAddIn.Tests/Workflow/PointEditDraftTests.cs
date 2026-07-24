using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class PointEditDraftTests
{
    public static void ValidateRejectsMissingAndInvalidNumericFields()
    {
        var draft = new PointEditDraft
        {
            RowId = "row-1",
            ParcelGroupId = "parcel-001",
            PointIdentifier = string.Empty,
            Easting = "abc",
            Northing = string.Empty,
            Length = "bad"
        };

        var errors = draft.Validate(Array.Empty<ExtractionReviewRowViewModel>());

        TestAssert.True(errors.Count >= 4, "Draft validation should flag missing point id, invalid easting, missing northing, and invalid length.");
    }

    public static void ValidateBlocksDuplicatePointIdWithinParcel()
    {
        var existing = new ExtractionReviewRowViewModel(
            new ExtractionReviewRow
            {
                RowId = "row-existing",
                ParcelGroupId = "parcel-001",
                ParcelName = "parcel-001",
                TraverseId = "parcel-001",
                PointIdentifier = "parcel-001_P3",
                Easting = "680000.000",
                Northing = "642000.000"
            },
            () => { });

        var draft = new PointEditDraft
        {
            RowId = "row-new",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "parcel-001_P3",
            Easting = "680001.000",
            Northing = "642001.000"
        };

        var errors = draft.Validate(new[] { existing });

        TestAssert.True(errors.Any(error => error.Contains("already used", StringComparison.OrdinalIgnoreCase)), "Draft validation should block duplicate point ids within the same parcel.");
    }

    public static void ApplyToWritesCommittedValuesToRow()
    {
        var row = new ExtractionReviewRow
        {
            RowId = "row-1",
            ParcelGroupId = "parcel-001",
            ParcelName = "parcel-001",
            TraverseId = "parcel-001"
        };

        var draft = new PointEditDraft
        {
            RowId = "row-1",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "parcel-001_P4",
            Easting = "680002.000",
            Northing = "642002.000",
            Length = "12.50",
            ExtractionStatus = "Adjusted",
            SourceEvidence = "Manual correction",
            Unresolved = true,
            UnresolvedReason = "Need second check",
            ReviewNotes = "Updated in modal"
        };

        draft.ApplyTo(row);

        TestAssert.Equal("parcel-001_P4", row.PointIdentifier, "Committed draft should update the point identifier.");
        TestAssert.Equal("680002.000", row.Easting, "Committed draft should update easting.");
        TestAssert.Equal("642002.000", row.Northing, "Committed draft should update northing.");
        TestAssert.Equal("12.50", row.Length, "Committed draft should update length.");
        TestAssert.Equal("Adjusted", row.ExtractionStatus, "Committed draft should update extraction status.");
        TestAssert.Equal("Manual correction", row.SourceEvidence, "Committed draft should update source evidence.");
        TestAssert.True(row.Unresolved, "Committed draft should update unresolved flag.");
        TestAssert.Equal("Need second check", row.UnresolvedReason, "Committed draft should update unresolved reason.");
        TestAssert.Equal("Updated in modal", row.ReviewNotes, "Committed draft should update review notes.");
    }

    public static void RowViewModelApplyCommittedEditNotifiesDerivedState()
    {
        var row = new ExtractionReviewRow
        {
            RowId = "row-1",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "P1",
            Easting = "1",
            Northing = "1"
        };
        var changedProperties = new List<string>();
        var viewModel = new ExtractionReviewRowViewModel(row, () => { });
        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.ApplyCommittedEdit(new PointEditDraft
        {
            RowId = "row-1",
            ParcelGroupId = "parcel-001",
            PointIdentifier = "P1",
            Easting = "2",
            Northing = "3",
            ExtractionStatus = "Adjusted"
        });

        TestAssert.True(changedProperties.Contains(nameof(ExtractionReviewRowViewModel.IsEdited)), "Point edit should notify derived edited state.");
        TestAssert.True(changedProperties.Contains(nameof(ExtractionReviewRowViewModel.HasMissingRequiredValues)), "Point edit should notify missing-required state.");
    }
}
