namespace ParcelWorkflowAddIn.Workflow;

internal enum WorkflowWorkspaceStage
{
    Intake,
    Preflight,
    ExtractionReview,
    Validation,
    Outputs,
    ReadyToComplete
}

internal static class WorkflowWorkspacePlanner
{
    public static WorkflowWorkspaceStage ResolveActiveStage(WorkflowState state, bool intakeReadyForPreflight, bool hasReviewArtifact)
    {
        return state switch
        {
            WorkflowState.NoCase => WorkflowWorkspaceStage.Intake,
            WorkflowState.Intake when intakeReadyForPreflight => WorkflowWorkspaceStage.Preflight,
            WorkflowState.Intake => WorkflowWorkspaceStage.Intake,
            WorkflowState.PreflightRunning or WorkflowState.PreflightBlocked => WorkflowWorkspaceStage.Preflight,
            WorkflowState.PreflightPassed => WorkflowWorkspaceStage.ExtractionReview,
            WorkflowState.ExtractionRunning or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending => WorkflowWorkspaceStage.ExtractionReview,
            WorkflowState.ReviewApproved or WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked => WorkflowWorkspaceStage.Validation,
            WorkflowState.ValidationPassed or WorkflowState.OutputRunning => WorkflowWorkspaceStage.Outputs,
            WorkflowState.OutputCreated => WorkflowWorkspaceStage.ReadyToComplete,
            _ => WorkflowWorkspaceStage.Intake
        };
    }
}
