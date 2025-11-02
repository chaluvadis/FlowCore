namespace FlowCore.Interfaces;
/// <summary>
/// Defines the core contract for workflow engines that can execute workflow definitions.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Executes a workflow asynchronously using the provided workflow definition and input data.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to execute.</param>
    /// <param name="input">The input data for the workflow execution.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the workflow execution result.</returns>
    Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition workflowDefinition,
        object input,
        CancellationToken ct = default);
    /// <summary>
    /// Resumes a workflow execution from a previously saved checkpoint.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to resume.</param>
    /// <param name="executionId">The execution ID to resume.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the resumed workflow execution result.</returns>
    Task<WorkflowExecutionResult> ResumeFromCheckpointAsync(
        WorkflowDefinition workflowDefinition,
        Guid executionId,
        CancellationToken ct = default);
    /// <summary>
    /// Suspends a running workflow execution and saves its current state.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A task representing the suspend operation.</returns>
    Task SuspendWorkflowAsync(string workflowId, Guid executionId, ExecutionContext context);
}
