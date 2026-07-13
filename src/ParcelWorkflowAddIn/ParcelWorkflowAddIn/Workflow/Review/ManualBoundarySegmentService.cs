namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ManualBoundarySegmentService
{
    public ExtractionReviewSegment CreateManualSegment(ExtractionReviewDocument document)
    {
        var nextSequence = document.Segments
            .Select(segment => segment.EffectiveSequence == int.MaxValue ? 0 : segment.EffectiveSequence)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return new ExtractionReviewSegment
        {
            SegmentId = BuildUniqueManualSegmentId(document, nextSequence),
            Sequence = nextSequence,
            ReviewSequence = nextSequence,
            IncludeInBoundary = true,
            ReviewIncludeInBoundary = true,
            Status = "Manual entry",
            ReviewStatus = "Manual entry",
            SourceEvidence = "Manual correction",
            IsEdited = true,
            OriginalValues = new ExtractionReviewSegmentOriginalValues
            {
                Sequence = nextSequence,
                IncludeInBoundary = true
            }
        };
    }

    private static string BuildUniqueManualSegmentId(ExtractionReviewDocument document, int sequence)
    {
        var baseId = $"manual-segment-{sequence:000}";
        var candidate = baseId;
        var suffix = 2;
        while (document.Segments.Any(segment => string.Equals(segment.SegmentId, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
