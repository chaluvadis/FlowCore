namespace LinkedListWorkflowEngine.Core.Interfaces;

/// <summary>
/// Defines the contract for execution monitors that track and respond to workflow execution events.
/// Implementations can log events, send notifications, update dashboards, or trigger other workflows.
/// </summary>
public interface IExecutionMonitor
{
    /// <summary>
    /// Called when a workflow execution starts.
    /// </summary>
    /// <param name="metadata">Metadata about the started workflow execution.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    Task OnWorkflowStartedAsync(WorkflowExecutionMetadata metadata);

    /// <summary>
    /// Called when a workflow block completes execution.
    /// </summary>
    /// <param name="info">Information about the executed block.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    Task OnBlockExecutedAsync(BlockExecutionInfo info);

    /// <summary>
    /// Called when a workflow execution completes successfully.
    /// </summary>
    /// <param name="result">The final result of the completed workflow execution.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    Task OnWorkflowCompletedAsync(WorkflowExecutionResult result);

    /// <summary>
    /// Called when a workflow execution fails with an error.
    /// </summary>
    /// <param name="result">The result of the failed workflow execution.</param>
    /// <param name="exception">The exception that caused the workflow to fail.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    Task OnWorkflowFailedAsync(WorkflowExecutionResult result, Exception exception);

    /// <summary>
    /// Called when a workflow execution is cancelled.
    /// </summary>
    /// <param name="metadata">Metadata about the cancelled workflow execution.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    Task OnWorkflowCancelledAsync(WorkflowExecutionMetadata metadata);
}

public record WorkflowExecutionMetadata
{
    public string WorkflowId { get; init; } = default!;

    public Guid ExecutionId { get; init; }

    public string WorkflowVersion { get; init; } = default!;

    public DateTime StartedAt { get; init; }

    public string CorrelationId { get; init; } = default!;

    public string? InitiatedBy { get; init; }
}