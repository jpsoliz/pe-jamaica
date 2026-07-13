using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ManualBoundarySegmentServiceTests
{
    public static void CreateManualSegmentUsesNextSequenceAndManualDefaults()
    {
        var document = new ExtractionReviewDocument();
        document.Segments.Add(new ExtractionReviewSegment
        {
            SegmentId = "segment-001",
            Sequence = 1,
            FromPoint = "1",
            ToPoint = "2",
            BearingText = "N01 00E",
            DistanceText = "10.000",
            IncludeInBoundary = true
        });

        var service = new ManualBoundarySegmentService();
        var segment = service.CreateManualSegment(document);

        TestAssert.Equal(2, segment.Sequence ?? -1, "Manual segment should default to the next available sequence.");
        TestAssert.Equal(2, segment.ReviewSequence ?? -1, "Manual segment review sequence should match the default sequence.");
        TestAssert.True(segment.IncludeInBoundary, "Manual segment should participate in boundary solving by default.");
        TestAssert.Equal<bool?>(true, segment.ReviewIncludeInBoundary, "Manual segment review include flag should be explicit.");
        TestAssert.Equal("Manual entry", segment.Status, "Manual segment should use a manual status.");
        TestAssert.Equal("Manual correction", segment.SourceEvidence, "Manual segment should preserve manual source evidence.");
        TestAssert.True(segment.IsEdited, "Manual segment should be marked edited so save persists it as a review change.");
        TestAssert.True(segment.SegmentId.StartsWith("manual-segment-002", StringComparison.Ordinal), "Manual segment id should be deterministic and sequence-based.");
    }
}
