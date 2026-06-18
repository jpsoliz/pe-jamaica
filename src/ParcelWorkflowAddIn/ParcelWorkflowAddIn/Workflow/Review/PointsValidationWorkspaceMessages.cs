namespace ParcelWorkflowAddIn.Workflow.Review;

internal static class PointsValidationWorkspaceMessages
{
    public static string BuildCloseStatusText(
        bool reviewSaved,
        bool continuedToCreateSpatialUnits,
        bool discardedUnsavedChanges,
        Workflow.WorkflowState currentState)
    {
        if (discardedUnsavedChanges)
        {
            return "Points Validation Tool closed without saving. Previous saved review data remains available.";
        }

        if (continuedToCreateSpatialUnits
            || currentState is Workflow.WorkflowState.ReviewApproved
                or Workflow.WorkflowState.ValidationRunning
                or Workflow.WorkflowState.ValidationBlocked
                or Workflow.WorkflowState.ValidationPassed
                or Workflow.WorkflowState.OutputRunning)
        {
            return "Points Validation Tool closed. Create Spatial Units is ready for the saved validated points.";
        }

        if (reviewSaved)
        {
            return "Points Validation Tool closed. Saved validated points remain available when you are ready to continue.";
        }

        return "Points Validation Tool closed. Continue point review whenever you are ready.";
    }
}
