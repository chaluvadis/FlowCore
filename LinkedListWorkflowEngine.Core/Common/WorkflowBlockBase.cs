namespace LinkedListWorkflowEngine.Core.Common;
public abstract class WorkflowBlockBase(ILogger? logger = null) : IWorkflowBlock
{
    protected ILogger? Logger => logger;
    public abstract string NextBlockOnSuccess { get; }
    public abstract string NextBlockOnFailure { get; }
    public virtual string BlockId => $"{GetType().Name}_{Guid.NewGuid()}";
    public virtual string DisplayName => GetType().Name;
    public virtual string Version => "1.0.0";
    public virtual string Description => $"Workflow block: {DisplayName}";
    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        try
        {
            logger?.LogInformation("Executing workflow block {BlockId} ({DisplayName})", BlockId, DisplayName);
            // Pre-execution validation
            if (!await CanExecuteAsync(context))
            {
                logger?.LogWarning("Pre-execution validation failed for block {BlockId}", BlockId);
                return ExecutionResult.Failure(NextBlockOnFailure, null, new InvalidOperationException("Pre-execution validation failed"));
            }
            // Execute the block implementation
            var result = await ExecuteBlockAsync(context);
            // Mark metadata as completed
            result.Metadata.MarkCompleted();
            logger?.LogInformation("Workflow block {BlockId} completed with status {Status}",
                BlockId, result.Status);
            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error executing workflow block {BlockId}", BlockId);
            return ExecutionResult.Failure(NextBlockOnFailure, null, ex);
        }
        finally
        {
            // Always perform cleanup
            try
            {
                var dummyResult = ExecutionResult.Failure(); // Create a dummy result for cleanup
                await CleanupAsync(context, dummyResult);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error during cleanup for workflow block {BlockId}", BlockId);
            }
        }
    }
    protected abstract Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context);
    public virtual Task<bool> CanExecuteAsync(ExecutionContext context) => Task.FromResult(true);
    public virtual Task CleanupAsync(ExecutionContext context, ExecutionResult result) => Task.CompletedTask;
    protected void LogInfo(string message, params object[] args) => logger?.LogInformation(message, args);
    protected void LogWarning(string message, params object[] args) => logger?.LogWarning(message, args);
    protected void LogError(Exception exception, string message, params object[] args) => logger?.LogError(exception, message, args);
    protected void LogDebug(string message, params object[] args) => logger?.LogDebug(message, args);
}