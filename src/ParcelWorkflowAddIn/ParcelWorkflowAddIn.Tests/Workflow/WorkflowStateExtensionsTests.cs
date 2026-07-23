using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class WorkflowStateExtensionsTests
{
    public static void DisplayNamesUseComputeWorkflowVocabulary()
    {
        TestAssert.Equal("Supporting Document Check", WorkflowState.Intake.ToDisplayName(), "Intake display name mismatch.");
        TestAssert.Equal("Structure Check Running", WorkflowState.PreflightRunning.ToDisplayName(), "Structure check running display name mismatch.");
        TestAssert.Equal("Validate Points Ready", WorkflowState.ReviewPending.ToDisplayName(), "Validate points ready display name mismatch.");
        TestAssert.Equal("Manual Mode", WorkflowState.ReviewManualPending.ToDisplayName(), "Manual Mode display name mismatch.");
        TestAssert.Equal("Create Spatial Units Ready", WorkflowState.ReviewApproved.ToDisplayName(), "Create spatial units ready display name mismatch.");
        TestAssert.Equal("Create Spatial Units Running", WorkflowState.OutputRunning.ToDisplayName(), "Create spatial units running display name mismatch.");
        TestAssert.Equal("Final Review Ready", WorkflowState.OutputCreated.ToDisplayName(), "Final review ready display name mismatch.");
        TestAssert.Equal("Finalize Ready", WorkflowState.SpatialReviewApproved.ToDisplayName(), "Finalize ready display name mismatch.");
    }
}
