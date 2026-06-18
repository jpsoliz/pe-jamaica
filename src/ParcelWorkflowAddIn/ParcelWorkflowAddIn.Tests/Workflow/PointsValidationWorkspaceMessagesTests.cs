using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class PointsValidationWorkspaceMessagesTests
{
    public static void CloseStatusUsesDiscardMessageWhenUnsavedChangesAreDropped()
    {
        var message = PointsValidationWorkspaceMessages.BuildCloseStatusText(
            reviewSaved: false,
            continuedToCreateSpatialUnits: false,
            discardedUnsavedChanges: true,
            WorkflowState.ReviewPending);

        TestAssert.Equal(
            "Points Validation Tool closed without saving. Previous saved review data remains available.",
            message,
            "Discarded unsaved changes message mismatch.");
    }

    public static void CloseStatusUsesCreateSpatialUnitsReadinessWhenContinueWasConfirmed()
    {
        var message = PointsValidationWorkspaceMessages.BuildCloseStatusText(
            reviewSaved: true,
            continuedToCreateSpatialUnits: true,
            discardedUnsavedChanges: false,
            WorkflowState.ReviewApproved);

        TestAssert.Equal(
            "Points Validation Tool closed. Create Spatial Units is ready for the saved validated points.",
            message,
            "Continue message mismatch.");
    }

    public static void CloseStatusUsesSavedOnlyMessageWhenPointsWereSavedButNotContinued()
    {
        var message = PointsValidationWorkspaceMessages.BuildCloseStatusText(
            reviewSaved: true,
            continuedToCreateSpatialUnits: false,
            discardedUnsavedChanges: false,
            WorkflowState.ReviewPending);

        TestAssert.Equal(
            "Points Validation Tool closed. Saved validated points remain available when you are ready to continue.",
            message,
            "Saved-only close message mismatch.");
    }
}
