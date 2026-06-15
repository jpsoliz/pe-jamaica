using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class WorkflowWorkspacePlannerTests
{
    public static void IntakeStatesResolveToIntakeWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.Intake, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.NoCase, false, false), "NoCase should focus intake.");
        TestAssert.Equal(WorkflowWorkspaceStage.Intake, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.Intake, false, false), "Intake without completed intake context should focus intake.");
    }

    public static void IntakeReadyForPreflightResolvesToPreflightWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.Preflight, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.Intake, true, false), "Intake with copied sources and detected profile should focus preflight.");
    }

    public static void PreflightStatesResolveToPreflightWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.Preflight, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.PreflightRunning, false, false), "Preflight running should focus preflight.");
        TestAssert.Equal(WorkflowWorkspaceStage.Preflight, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.PreflightBlocked, false, false), "Preflight blocked should focus preflight.");
    }

    public static void ReviewStatesResolveToExtractionWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.ExtractionReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.PreflightPassed, false, false), "Preflight passed should immediately focus extraction review.");
        TestAssert.Equal(WorkflowWorkspaceStage.ExtractionReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.PreflightPassed, false, true), "Preflight passed with review artifact should focus extraction review.");
        TestAssert.Equal(WorkflowWorkspaceStage.ExtractionReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ExtractionRunning, false, true), "Extraction running should focus extraction review.");
        TestAssert.Equal(WorkflowWorkspaceStage.ExtractionReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ExtractionFailed, false, true), "Extraction failed should focus extraction review.");
        TestAssert.Equal(WorkflowWorkspaceStage.ExtractionReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ReviewPending, false, true), "Review pending should focus extraction review.");
    }

    public static void ValidationStatesResolveToValidationWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.Validation, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ReviewApproved, false, true), "Approved review should focus validation.");
        TestAssert.Equal(WorkflowWorkspaceStage.Validation, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ValidationRunning, false, true), "Validation running should focus validation.");
        TestAssert.Equal(WorkflowWorkspaceStage.Validation, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ValidationBlocked, false, true), "Validation blocked should focus validation.");
    }

    public static void ValidationPassedResolvesToOutputsWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.Outputs, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ValidationPassed, false, true), "Validation passed should focus outputs.");
        TestAssert.Equal(WorkflowWorkspaceStage.Outputs, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.OutputRunning, false, true), "Output running should keep focus on outputs.");
    }

    public static void OutputCreatedResolvesToSpatialReviewWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.SpatialReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.OutputCreated, false, true), "Created outputs should focus spatial review.");
        TestAssert.Equal(WorkflowWorkspaceStage.SpatialReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.SpatialReviewPending, false, true), "Pending spatial review should focus spatial review.");
    }

    public static void SpatialReviewApprovedResolvesToReadyToCompleteWorkspace()
    {
        TestAssert.Equal(WorkflowWorkspaceStage.ReadyToComplete, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.SpatialReviewApproved, false, true), "Approved spatial review should focus ready to complete.");
    }
}
