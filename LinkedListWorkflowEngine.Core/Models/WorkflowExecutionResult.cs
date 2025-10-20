namespace LinkedListWorkflowEngine.Core.Models;
public class WorkflowExecutionResult
{
    public string WorkflowId { get; internal set; } = string.Empty;
    public string WorkflowVersion { get; internal set; } = string.Empty;
    public Guid ExecutionId { get; internal set; }
    public DateTime StartedAt { get; internal set; }
    public DateTime? CompletedAt { get; internal set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    public WorkflowStatus Status { get; internal set; }
    public bool Succeeded { get; internal set; }
    public IDictionary<string, object>? FinalState { get; internal set; }
    public Exception? Error { get; internal set; }
}