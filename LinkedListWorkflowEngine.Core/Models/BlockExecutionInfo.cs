namespace LinkedListWorkflowEngine.Core.Models;

public class BlockExecutionInfo
{
    public string BlockName { get; internal set; } = string.Empty;
    public string BlockId { get; internal set; } = string.Empty;
    public string BlockType { get; internal set; } = string.Empty;
    public DateTime StartedAt { get; internal set; }
    public DateTime CompletedAt { get; internal set; }
    public ExecutionStatus Status { get; internal set; }
    public string? NextBlockName { get; internal set; }
    public object? Output { get; internal set; }
}