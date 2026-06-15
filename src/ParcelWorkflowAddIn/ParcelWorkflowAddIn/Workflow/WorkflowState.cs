namespace ParcelWorkflowAddIn.Workflow;

public enum WorkflowState
{
    NoCase,
    Intake,
    PreflightRunning,
    PreflightBlocked,
    PreflightPassed,
    ExtractionRunning,
    ExtractionFailed,
    ReviewPending,
    ReviewApproved,
    ValidationRunning,
    ValidationBlocked,
    ValidationPassed,
    OutputRunning,
    OutputCreated,
    SpatialReviewPending,
    SpatialReviewApproved
}
