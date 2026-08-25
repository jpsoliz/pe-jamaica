using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Intake;

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
        TestAssert.Equal(WorkflowWorkspaceStage.ExtractionReview, WorkflowWorkspacePlanner.ResolveActiveStage(WorkflowState.ReviewManualPending, false, true), "Manual review pending should keep focus on extraction review.");
    }

    public static void PlaPreflightPassedWithoutSelectionResolvesToPlanEvidenceSelection()
    {
        TestAssert.Equal(
            WorkflowWorkspaceStage.PlaPlanEvidenceSelection,
            WorkflowWorkspacePlanner.ResolveProfileActiveStage(
                WorkflowState.PreflightPassed,
                false,
                false,
                SourceInputProfile.PlaPlanAnnexation,
                hasPlaPlanEvidenceSelection: false),
            "PLA should focus Select Plan Evidence before extraction when no saved selection exists.");
    }

    public static void PlaPreflightBlockedReadyForEvidenceResolvesToPlanEvidenceSelection()
    {
        TestAssert.Equal(
            WorkflowWorkspaceStage.PlaPlanEvidenceSelection,
            WorkflowWorkspacePlanner.ResolveProfileActiveStage(
                WorkflowState.PreflightBlocked,
                true,
                false,
                SourceInputProfile.PlaPlanAnnexation,
                hasPlaPlanEvidenceSelection: false,
                plaReadyForPlanEvidenceSelection: true),
            "PLA should focus Select Plan Evidence when Structure Check and plan annexation PDF are valid even if deferred evidence keeps preflight blocked.");
    }

    public static void PlaPreflightBlockedWithSelectionResolvesToExtractionWorkspace()
    {
        TestAssert.Equal(
            WorkflowWorkspaceStage.ExtractionReview,
            WorkflowWorkspacePlanner.ResolveProfileActiveStage(
                WorkflowState.PreflightBlocked,
                true,
                false,
                SourceInputProfile.PlaPlanAnnexation,
                hasPlaPlanEvidenceSelection: true,
                plaReadyForPlanEvidenceSelection: true),
            "PLA should move from Select Plan Evidence to extraction after the evidence artifact exists.");
    }

    public static void PlaPreflightBlockedNotReadyKeepsPreflightWorkspace()
    {
        TestAssert.Equal(
            WorkflowWorkspaceStage.Preflight,
            WorkflowWorkspacePlanner.ResolveProfileActiveStage(
                WorkflowState.PreflightBlocked,
                true,
                false,
                SourceInputProfile.PlaPlanAnnexation,
                hasPlaPlanEvidenceSelection: false,
                plaReadyForPlanEvidenceSelection: false),
            "PLA should keep the normal preflight route until Structure Check and the plan annexation PDF are valid.");
    }

    public static void PlaWithSelectionResolvesToExtractionWorkspace()
    {
        TestAssert.Equal(
            WorkflowWorkspaceStage.ExtractionReview,
            WorkflowWorkspacePlanner.ResolveProfileActiveStage(
                WorkflowState.PreflightPassed,
                false,
                false,
                SourceInputProfile.PlaPlanAnnexation,
                hasPlaPlanEvidenceSelection: true),
            "PLA should focus extraction once selected plan evidence is saved.");
    }

    public static void NonPlaProfileKeepsDefaultExtractionWorkspace()
    {
        TestAssert.Equal(
            WorkflowWorkspaceStage.ExtractionReview,
            WorkflowWorkspacePlanner.ResolveProfileActiveStage(
                WorkflowState.PreflightPassed,
                false,
                false,
                SourceInputProfile.PxaSurveyPlan,
                hasPlaPlanEvidenceSelection: false),
            "Non-PLA profiles should keep existing extraction workspace routing.");
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
