namespace FlowCore.Interfaces;

/// <summary>
/// Defines the contract for workflow executors that handle the core execution logic of workflow definitions.
/// Implementations of this interface are responsible for processing workflow blocks, managing execution state,
/// and handling workflow lifecycle events including suspension and resumption.
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// Executes a workflow definition asynchronously from the beginning.
    /// </summary>
    /// <param name="definition">The workflow definition to execute.</param>
    /// <param name="initialContext">The initial execution context containing input data and configuration.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the workflow execution.</param>
    /// <returns>A task representing the workflow execution result with final state and status.</returns>
    Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        ExecutionContext initialContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a workflow execution from a previously saved checkpoint.
    /// </summary>
    /// <param name="definition">The workflow definition to resume execution for.</param>
    /// <param name="executionId">The unique identifier of the execution to resume.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the resumed execution.</param>
    /// <returns>A task representing the resumed workflow execution result.</returns>
    Task<WorkflowExecutionResult> ResumeAsync(
        WorkflowDefinition definition,
        Guid executionId,
        CancellationToken cancellationToken = default);
}