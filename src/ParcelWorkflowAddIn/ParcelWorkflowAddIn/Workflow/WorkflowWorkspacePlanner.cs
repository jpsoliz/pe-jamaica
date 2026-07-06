namespace ParcelWorkflowAddIn.Workflow;

internal enum WorkflowWorkspaceStage
{
    Intake,
    Preflight,
    StructureCheck,
    GeoreferenceCheck,
    DimensionCheck,
    ExtractionReview,
    Validation,
    Outputs,
    SpatialReview,
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
            WorkflowState.ExtractionRunning or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending or WorkflowState.ReviewManualPending => WorkflowWorkspaceStage.ExtractionReview,
            WorkflowState.ReviewApproved or WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked => WorkflowWorkspaceStage.Validation,
            WorkflowState.ValidationPassed or WorkflowState.OutputRunning => WorkflowWorkspaceStage.Outputs,
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => WorkflowWorkspaceStage.SpatialReview,
            WorkflowState.SpatialReviewApproved => WorkflowWorkspaceStage.ReadyToComplete,
            _ => WorkflowWorkspaceStage.Intake
        };
    }
}
