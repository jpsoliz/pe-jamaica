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
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workflow state.")
        };
    }
}
