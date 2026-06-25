namespace ParcelWorkflowAddIn.Workflow;

public static class WorkflowStateExtensions
{
    public static string ToContractValue(this WorkflowState state)
    {
        return state switch
        {
            WorkflowState.NoCase => "no_case",
            WorkflowState.Intake => "intake",
            WorkflowState.PreflightRunning => "preflight_running",
            WorkflowState.PreflightBlocked => "preflight_blocked",
            WorkflowState.PreflightPassed => "preflight_passed",
            WorkflowState.ExtractionRunning => "extraction_running",
            WorkflowState.ExtractionFailed => "extraction_failed",
            WorkflowState.ReviewPending => "review_pending",
            WorkflowState.ReviewManualPending => "review_manual_pending",
            WorkflowState.ReviewApproved => "review_approved",
            WorkflowState.ValidationRunning => "validation_running",
            WorkflowState.ValidationBlocked => "validation_blocked",
            WorkflowState.ValidationPassed => "validation_passed",
            WorkflowState.OutputRunning => "output_running",
            WorkflowState.OutputCreated => "output_created",
            WorkflowState.SpatialReviewPending => "spatial_review_pending",
            WorkflowState.SpatialReviewApproved => "spatial_review_approved",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workflow state.")
        };
    }

    public static string ToDisplayName(this WorkflowState state)
    {
        return state switch
        {
            WorkflowState.NoCase => "No Case",
            WorkflowState.Intake => "Supporting Document Check",
            WorkflowState.PreflightRunning => "Structure Check Running",
            WorkflowState.PreflightBlocked => "Structure / Georeference Check Blocked",
            WorkflowState.PreflightPassed => "Georeference Check Ready",
            WorkflowState.ExtractionRunning => "Validate Points Running",
            WorkflowState.ExtractionFailed => "Validate Points Blocked",
            WorkflowState.ReviewPending => "Validate Points Ready",
            WorkflowState.ReviewManualPending => "Manual Review Workspace Preparing",
            WorkflowState.ReviewApproved => "Create Spatial Units Ready",
            WorkflowState.ValidationRunning => "Create Spatial Units Running",
            WorkflowState.ValidationBlocked => "Create Spatial Units Blocked",
            WorkflowState.ValidationPassed => "Create Spatial Units Ready",
            WorkflowState.OutputRunning => "Create Spatial Units Running",
            WorkflowState.OutputCreated => "Final Review Ready",
            WorkflowState.SpatialReviewPending => "Final Review Pending",
            WorkflowState.SpatialReviewApproved => "Finalize Ready",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workflow state.")
        };
    }
}
