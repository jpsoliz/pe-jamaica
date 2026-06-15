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
            WorkflowState.Intake => "Intake",
            WorkflowState.PreflightRunning => "Preflight Running",
            WorkflowState.PreflightBlocked => "Preflight Blocked",
            WorkflowState.PreflightPassed => "Preflight Passed",
            WorkflowState.ExtractionRunning => "Extraction Running",
            WorkflowState.ExtractionFailed => "Extraction Failed",
            WorkflowState.ReviewPending => "Review Pending",
            WorkflowState.ReviewApproved => "Review Approved",
            WorkflowState.ValidationRunning => "Validation Running",
            WorkflowState.ValidationBlocked => "Validation Blocked",
            WorkflowState.ValidationPassed => "Validation Passed",
            WorkflowState.OutputRunning => "Output Running",
            WorkflowState.OutputCreated => "Output Created",
            WorkflowState.SpatialReviewPending => "Spatial Review Pending",
            WorkflowState.SpatialReviewApproved => "Spatial Review Approved",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workflow state.")
        };
    }
}
