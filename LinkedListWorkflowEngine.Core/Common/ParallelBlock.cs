namespace LinkedListWorkflowEngine.Core.Common;
/// <summary>
/// Workflow block that executes multiple child blocks in parallel.
/// Supports different execution modes (all, any, majority) and result aggregation.
/// </summary>
public class ParallelBlock : WorkflowBlockBase
{
    private readonly ParallelExecutionConfig _config;
    private readonly IWorkflowBlockFactory _blockFactory;
    /// <summary>
    /// Gets the name of the next block to execute on successful completion.
    /// </summary>
    public override string NextBlockOnSuccess { get; }
    /// <summary>
    /// Gets the name of the next block to execute on failure.
    /// </summary>
    public override string NextBlockOnFailure { get; }
    /// <summary>
    /// Gets the block IDs to execute in parallel.
    /// </summary>
    public IReadOnlyList<string> ParallelBlockIds { get; }
    /// <summary>
    /// Initializes a new instance of the ParallelBlock class.
    /// </summary>
    /// <param name="parallelBlockIds">The block IDs to execute in parallel.</param>
    /// <param name="executionMode">The execution mode for parallel blocks.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="blockFactory">Factory for creating child blocks.</param>
    /// <param name="logger">Optional logger.</param>
    public ParallelBlock(
        IEnumerable<string> parallelBlockIds,
        ParallelExecutionMode executionMode = ParallelExecutionMode.All,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        IWorkflowBlockFactory? blockFactory = null,
        ILogger? logger = null) : base(logger)
    {
        ParallelBlockIds = new List<string>(parallelBlockIds ?? throw new ArgumentNullException(nameof(parallelBlockIds)));
        NextBlockOnSuccess = nextBlockOnSuccess;
        NextBlockOnFailure = nextBlockOnFailure;
        _blockFactory = blockFactory ?? new WorkflowBlockFactory(new ServiceCollection().BuildServiceProvider());
        _config = new ParallelExecutionConfig
        {
            Mode = executionMode,
            MaxConcurrency = ParallelBlockIds.Count,
            Timeout = TimeSpan.FromMinutes(5),
            FailFast = false
        };
    }
    /// <summary>
    /// Executes the core logic of the workflow block.
    /// </summary>
    /// <param name="context">The execution context containing input data, state, and services.</param>
    /// <returns>An execution result indicating the outcome and next block to execute.</returns>
    protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
    {
        if (!ParallelBlockIds.Any())
        {
            LogWarning("No parallel blocks specified, skipping parallel execution");
            return ExecutionResult.Success(NextBlockOnSuccess);
        }
        LogInfo($"Starting parallel execution of {ParallelBlockIds.Count} blocks with mode {_config.Mode}");
        var executionTasks = new List<Task<BlockExecutionResult>>();
        var cancellationTokenSource = new CancellationTokenSource();
        var timeoutTokenSource = new CancellationTokenSource(_config.Timeout);
        // Combine cancellation tokens
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken,
            timeoutTokenSource.Token).Token;
        try
        {
            // Start all parallel executions
            foreach (var blockId in ParallelBlockIds)
            {
                var task = ExecuteBlockInParallelAsync(blockId, context, combinedToken);
                executionTasks.Add(task);
            }
            // Wait for completion based on execution mode
            var results = await WaitForCompletionAsync(executionTasks, combinedToken);
            // Determine overall result based on execution mode
            return AggregateResults(results);
        }
        catch (OperationCanceledException) when (timeoutTokenSource.Token.IsCancellationRequested)
        {
            LogWarning($"Parallel execution timed out after {_config.Timeout}");
            return ExecutionResult.Failure(NextBlockOnFailure, null, new TimeoutException("Parallel execution timeout"));
        }
        catch (OperationCanceledException)
        {
            LogWarning("Parallel execution was cancelled");
            return ExecutionResult.Failure(NextBlockOnFailure, null, new OperationCanceledException("Parallel execution cancelled"));
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during parallel execution");
            return ExecutionResult.Failure(NextBlockOnFailure, null, ex);
        }
        finally
        {
            // Clean up cancellation sources
            cancellationTokenSource.Dispose();
            timeoutTokenSource.Dispose();
        }
    }
    /// <summary>
    /// Executes a single block in parallel.
    /// </summary>
    private async Task<BlockExecutionResult> ExecuteBlockInParallelAsync(
        string blockId,
        ExecutionContext originalContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a new context for this parallel execution
            var parallelContext = new ExecutionContext(
                originalContext.Input,
                originalContext.ServiceProvider,
                cancellationToken,
                originalContext.WorkflowName);
            // Copy state from original context
            foreach (var kvp in originalContext.State)
            {
                parallelContext.SetState(kvp.Key, kvp.Value);
            }
            // PLACEHOLDER: Get block definition from workflow
            // In a real implementation, this would come from the workflow definition
            var blockDefinition = new WorkflowBlockDefinition(
                blockId,
                "BasicBlocks.LogBlock", // PLACEHOLDER: This should be determined by the workflow
                "LinkedListWorkflowEngine.Core",
                NextBlockOnSuccess,
                NextBlockOnFailure);
            var block = _blockFactory.CreateBlock(blockDefinition);
            if (block == null)
            {
                throw new InvalidOperationException($"Failed to create parallel block '{blockId}'");
            }
            var result = await block.ExecuteAsync(parallelContext);
            return new BlockExecutionResult
            {
                BlockId = blockId,
                Success = result.IsSuccess,
                Result = result,
                ExecutionTime = result.Metadata.Duration,
                Error = null
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "Error executing parallel block {BlockId}", blockId);
            return new BlockExecutionResult
            {
                BlockId = blockId,
                Success = false,
                Result = ExecutionResult.Failure(NextBlockOnFailure, null, ex),
                ExecutionTime = TimeSpan.Zero,
                Error = ex
            };
        }
    }
    /// <summary>
    /// Waits for parallel executions to complete based on the execution mode.
    /// </summary>
    private async Task<List<BlockExecutionResult>> WaitForCompletionAsync(
        List<Task<BlockExecutionResult>> tasks,
        CancellationToken cancellationToken)
    {
        var results = new List<BlockExecutionResult>();
        var pendingTasks = new List<Task<BlockExecutionResult>>(tasks);
        while (pendingTasks.Count != 0)
        {
            // Wait for any task to complete
            var completedTask = await Task.WhenAny(pendingTasks);
            // Remove the completed task
            pendingTasks.Remove(completedTask);
            // Get the result
            var result = await completedTask;
            results.Add(result);
            LogDebug("Parallel block {BlockId} completed with status {Status}",
                result.BlockId, result.Success ? "Success" : "Failed");
            // Check if we should stop based on execution mode
            if (ShouldStopEarly(results, pendingTasks.Count))
            {
                LogInfo("Stopping parallel execution early based on mode {_config.Mode}", _config.Mode);
                // Cancel remaining tasks
                foreach (var task in pendingTasks)
                {
                    // Note: In a real implementation, you'd need a way to cancel the individual tasks
                    // This is a simplified version
                }
                break;
            }
        }
        return results;
    }
    /// <summary>
    /// Determines if execution should stop early based on the execution mode.
    /// </summary>
    private bool ShouldStopEarly(List<BlockExecutionResult> completedResults, int remainingTasks)
    {
        if (_config.FailFast && completedResults.Any(r => !r.Success))
        {
            return true; // Stop on first failure
        }
        return _config.Mode switch
        {
            ParallelExecutionMode.Any =>
                completedResults.Any(r => r.Success), // Stop when any succeeds
            ParallelExecutionMode.Majority =>
                completedResults.Count(r => r.Success) > (completedResults.Count + remainingTasks) / 2, // Stop when majority succeeds
            _ => false // All mode - continue until all complete
        };
    }
    /// <summary>
    /// Aggregates results from parallel executions based on the execution mode.
    /// </summary>
    private ExecutionResult AggregateResults(List<BlockExecutionResult> results)
    {
        var successfulResults = results.Where(r => r.Success).ToList();
        var failedResults = results.Where(r => !r.Success).ToList();
        // Store parallel execution results in context state
        var context = new ExecutionContext(
            successfulResults.Count, // Store count of successful results
            new ServiceCollection().BuildServiceProvider(),
            cancellationToken: default,
            workflowName: "ParallelExecution");
        context.SetState("ParallelResults", results);
        context.SetState("SuccessfulBlocks", successfulResults.Select(r => r.BlockId).ToList());
        context.SetState("FailedBlocks", failedResults.Select(r => r.BlockId).ToList());
        context.SetState("TotalExecutionTime", results.Sum(r => r.ExecutionTime.TotalMilliseconds));
        return _config.Mode switch
        {
            ParallelExecutionMode.All =>
                failedResults.Count != 0
                    ? ExecutionResult.Failure(NextBlockOnFailure, successfulResults.Count, new AggregateException(failedResults.Select(r => r.Error!)))
                    : ExecutionResult.Success(NextBlockOnSuccess, successfulResults.Count),
            ParallelExecutionMode.Any =>
                successfulResults.Count != 0
                    ? ExecutionResult.Success(NextBlockOnSuccess, successfulResults.Count)
                    : ExecutionResult.Failure(NextBlockOnFailure, 0, new InvalidOperationException("No parallel blocks succeeded")),
            ParallelExecutionMode.Majority =>
                successfulResults.Count > failedResults.Count
                    ? ExecutionResult.Success(NextBlockOnSuccess, successfulResults.Count)
                    : ExecutionResult.Failure(NextBlockOnFailure, successfulResults.Count, new InvalidOperationException("Majority of parallel blocks failed")),
            _ => ExecutionResult.Success(NextBlockOnSuccess, successfulResults.Count)
        };
    }
}
/// <summary>
/// Configuration for parallel block execution.
/// </summary>
public class ParallelExecutionConfig
{
    /// <summary>
    /// Gets the execution mode for parallel blocks.
    /// </summary>
    public ParallelExecutionMode Mode { get; set; } = ParallelExecutionMode.All;
    /// <summary>
    /// Gets the maximum number of concurrent executions.
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;
    /// <summary>
    /// Gets the timeout for the entire parallel execution.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>
    /// Gets whether to fail fast on first error.
    /// </summary>
    public bool FailFast { get; set; } = false;
    /// <summary>
    /// Gets the delay between starting parallel executions.
    /// </summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;
}
/// <summary>
/// Modes for parallel block execution.
/// </summary>
public enum ParallelExecutionMode
{
    /// <summary>
    /// All blocks must succeed for overall success.
    /// </summary>
    All,
    /// <summary>
    /// Any block succeeding results in overall success.
    /// </summary>
    Any,
    /// <summary>
    /// Majority of blocks must succeed for overall success.
    /// </summary>
    Majority
}
/// <summary>
/// Result of executing a single block in parallel.
/// </summary>
internal class BlockExecutionResult
{
    /// <summary>
    /// Gets the block identifier.
    /// </summary>
    public string BlockId { get; set; } = string.Empty;
    /// <summary>
    /// Gets a value indicating whether the block execution succeeded.
    /// </summary>
    public bool Success { get; set; }
    /// <summary>
    /// Gets the execution result.
    /// </summary>
    public ExecutionResult Result { get; set; } = ExecutionResult.Failure();
    /// <summary>
    /// Gets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
    /// <summary>
    /// Gets the error that occurred, if any.
    /// </summary>
    public Exception? Error { get; set; }
}