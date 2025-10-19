namespace LinkedListWorkflowEngine.Core.Interfaces;
public interface IWorkflowBlock
{
    Task<ExecutionResult> ExecuteAsync(ExecutionContext context);
    Task<bool> CanExecuteAsync(ExecutionContext context);
    Task CleanupAsync(ExecutionContext context, ExecutionResult result);
    string NextBlockOnSuccess { get; }
    string NextBlockOnFailure { get; }
    string BlockId { get; }
    string DisplayName { get; }
    string Version { get; }
    string Description { get; }
}