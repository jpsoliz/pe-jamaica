namespace ParcelWorkflowAddIn.Workflow.Execution;

public interface IWorkflowScriptAdapter
{
    string AdapterId { get; }

    Task<WorkflowScriptStepExecutionResult> ExecuteAsync(WorkflowScriptExecutionContext context, CancellationToken cancellationToken = default);
}
