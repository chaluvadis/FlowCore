namespace FlowCore.Workflow;
/// <summary>
/// Executes workflow definitions by processing workflow blocks sequentially.
/// Handles the core execution logic including state management, error handling, checkpointing, and block transitions.
/// Supports workflow suspension, resumption, and comprehensive execution monitoring.
/// </summary>
public class WorkflowExecutor : IWorkflowExecutor
{
    private readonly IWorkflowBlockFactory _blockFactory;
    private readonly IWorkflowStore _workflowStore;
    private readonly IExecutionMonitor? _monitor;
    private readonly ILogger<WorkflowExecutor>? _logger;
    private readonly ErrorHandler _errorHandler;
    private readonly GuardFactory _guardFactory;
    private readonly GuardManager _guardManager;
    /// <summary>
    /// Initializes a new instance of the WorkflowExecutor with the specified dependencies.
    /// </summary>
    /// <param name="blockFactory">Factory responsible for creating workflow block instances from definitions.</param>
    /// <param name="workflowStore">Storage mechanism for persisting workflow state and checkpoints.</param>
    /// <param name="monitor">Optional execution monitor for tracking workflow and block execution events.</param>
    /// <param name="logger">Optional logger for recording execution details and debugging information.</param>
    /// <param name="guardFactory">Factory for creating guard instances.</param>
    /// <param name="guardManager">Manager for evaluating guards.</param>
    /// <exception cref="ArgumentNullException">Thrown when blockFactory or workflowStore is null.</exception>
    private readonly int _checkpointInterval;
    public WorkflowExecutor(
        IWorkflowBlockFactory blockFactory,
        IWorkflowStore workflowStore,
        IExecutionMonitor? monitor = null,
        ILogger<WorkflowExecutor>? logger = null,
        GuardFactory? guardFactory = null,
        GuardManager? guardManager = null,
        int checkpointInterval = 5)
    {
        _blockFactory = blockFactory ?? throw new ArgumentNullException(nameof(blockFactory));
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _monitor = monitor;
        _logger = logger;
        _errorHandler = new ErrorHandler(_logger as ILogger<ErrorHandler> ?? new LoggerFactory().CreateLogger<ErrorHandler>());
        _guardFactory = guardFactory ?? new GuardFactory(new ServiceCollection().BuildServiceProvider(), logger as ILogger<GuardFactory>);
        _guardManager = guardManager ?? new GuardManager(logger as ILogger<GuardManager>);
        _checkpointInterval = checkpointInterval;
    }
    /// <summary>
    /// Executes a workflow definition asynchronously from the beginning.
    /// This method handles the complete workflow lifecycle including initialization, execution, and cleanup.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to execute.</param>
    /// <param name="initialContext">The initial execution context containing input data and configuration.</param>
    /// <param name="ct">Token that can be used to cancel the workflow execution.</param>
    /// <returns>A task representing the workflow execution result with final state and status.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowDefinition or initialContext is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the execution is cancelled via the cancellation token.</exception>
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition workflowDefinition,
        ExecutionContext initialContext,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        ArgumentNullException.ThrowIfNull(initialContext);
        // Generate unique execution identifier and create execution metadata
        var executionId = Guid.NewGuid();
        var metadata = new WorkflowExecutionMetadata
        {
            WorkflowId = workflowDefinition.Id,
            ExecutionId = executionId,
            WorkflowVersion = workflowDefinition.Version,
            StartedAt = DateTime.UtcNow,
            CorrelationId = initialContext.ExecutionId.ToString()
        };
        // Notify execution monitor that workflow execution has started
        if (_monitor != null)
        {
            await _monitor.OnWorkflowStartedAsync(metadata).ConfigureAwait(false);
        }
        // Initialize execution result with starting state
        var executionResult = new WorkflowExecutionResult
        {
            WorkflowId = workflowDefinition.Id,
            WorkflowVersion = workflowDefinition.Version,
            ExecutionId = executionId,
            StartedAt = DateTime.UtcNow,
            Status = WorkflowStatus.Running
        };
        try
        {
            // Create persistent execution record in the workflow store
            await _workflowStore.CreateExecutionAsync(workflowDefinition.Id, executionId, initialContext).ConfigureAwait(false);
            // Execute the workflow and capture final state
            var finalState = await ExecuteWorkflowInternalAsync(workflowDefinition, initialContext, executionId).ConfigureAwait(false);
            // Update execution result with successful completion
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Completed;
            executionResult.FinalState = finalState;
            executionResult.Succeeded = true;
            // Notify execution monitor that workflow completed successfully
            if (_monitor != null)
            {
                await _monitor.OnWorkflowCompletedAsync(executionResult).ConfigureAwait(false);
            }
            _logger?.LogWorkflowCompletion(workflowDefinition.Id, executionResult.Duration, true);
            return executionResult;
        }
        // Handle workflow cancellation
        catch (OperationCanceledException)
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Cancelled;
            executionResult.Succeeded = false;
            // Notify execution monitor that workflow was cancelled
            if (_monitor != null)
            {
                await _monitor.OnWorkflowCancelledAsync(metadata).ConfigureAwait(false);
            }
            _logger?.LogWorkflowCompletion(workflowDefinition.Id, executionResult.Duration, false);
            throw;
        }
        // Handle workflow execution errors with comprehensive error handling
        catch (Exception ex)
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Failed;
            executionResult.Succeeded = false;
            executionResult.Error = ex;
            // Determine which block caused the error and apply error handling strategy
            var blockName = initialContext.CurrentBlockName ?? "Unknown";
            var errorHandlingResult = await _errorHandler.HandleErrorAsync(
                ex,
                initialContext,
                blockName,
                workflowDefinition.ExecutionConfig.RetryPolicy).ConfigureAwait(false);
            await NotifyWorkflowFailedAsync(executionResult, ex, _monitor).ConfigureAwait(false);
            // Apply error handling strategy based on the error handler's recommendation
            if (errorHandlingResult.Action == ErrorHandlingAction.Fail)
            {
                throw;
            }
            else if (errorHandlingResult.Action == ErrorHandlingAction.Skip)
            {
                _logger?.LogWarning("Skipping failed block {BlockName} and continuing workflow", blockName);
                // For skip, we should continue to the next block instead of throwing
                // But since we are in the catch block, we need to handle this differently
                // For now, set the result to succeeded and continue
                executionResult.Succeeded = false;
                executionResult.Status = WorkflowStatus.Completed;
                executionResult.CompletedAt = DateTime.UtcNow;
                return executionResult;
            }
            return executionResult;
        }
    }
    /// <summary>
    /// Resumes a workflow execution from a previously saved checkpoint.
    /// This method loads the execution state from persistent storage and continues execution from where it left off.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to resume execution for.</param>
    /// <param name="executionId">The unique identifier of the execution to resume.</param>
    /// <param name="ct">Token that can be used to cancel the resumed execution.</param>
    /// <returns>A task representing the resumed workflow execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowDefinition is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no checkpoint is found for the specified execution.</exception>
    public async Task<WorkflowExecutionResult> ResumeAsync(
        WorkflowDefinition workflowDefinition,
        Guid executionId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        // Load the latest checkpoint for this execution from persistent storage
        var checkpoint = await _workflowStore.LoadLatestCheckpointAsync(
            workflowDefinition.Id,
            executionId,
            ct).ConfigureAwait(false);
        if (checkpoint == null)
        {
            throw new InvalidOperationException($"No checkpoint found for workflow {workflowDefinition.Id}, execution {executionId}");
        }
        // Reconstruct execution context from the checkpoint data
        var context = new ExecutionContext(
            input: checkpoint.OriginalInput ?? new object(),
            ct: ct,
            workflowName: workflowDefinition.Name)
        {
            CurrentBlockName = checkpoint.CurrentBlockName
        };
        // Restore the execution state from the checkpoint
        context.RestoreStateSnapshot(checkpoint.State);
        _logger?.LogInformation("Resuming workflow {WorkflowId} from checkpoint, execution {ExecutionId}",
            workflowDefinition.Id, executionId);
        // Continue workflow execution from the restored state
        var finalState = await ExecuteWorkflowInternalAsync(workflowDefinition, context, executionId).ConfigureAwait(false);
        // Create execution result for the resumed workflow
        var executionResult = new WorkflowExecutionResult
        {
            WorkflowId = workflowDefinition.Id,
            WorkflowVersion = workflowDefinition.Version,
            ExecutionId = executionId,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = WorkflowStatus.Completed,
            FinalState = finalState,
            Succeeded = true
        };
        // Notify execution monitor that workflow completed after resumption
        if (_monitor != null)
        {
            await _monitor.OnWorkflowCompletedAsync(executionResult).ConfigureAwait(false);
        }
        _logger?.LogInformation("Workflow {WorkflowId} resumed and completed successfully", workflowDefinition.Id);
        return executionResult;
    }
    /// <summary>
    /// Internal method that executes the core workflow logic by processing blocks sequentially.
    /// This method implements the main workflow execution loop, handling block transitions, state management, and checkpointing.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition containing the blocks to execute.</param>
    /// <param name="context">The execution context containing state and configuration.</param>
    /// <param name="executionId">The unique identifier for this execution instance.</param>
    /// <returns>A task representing the final workflow state as a dictionary of key-value pairs.</returns>
    private async Task<IDictionary<string, object>> ExecuteWorkflowInternalAsync(
        WorkflowDefinition workflowDefinition,
        ExecutionContext context,
        Guid executionId)
    {
        // Initialize execution from the workflow's start block
        var currentBlockName = workflowDefinition.StartBlockName;
        var executionHistory = new List<BlockExecutionInfo>();
        var checkpointCounter = 0;
        var checkpointInterval = _checkpointInterval; // Save checkpoint every configured blocks (now configurable)
        var previousState = new Dictionary<string, object>(context.State);
        // Main workflow execution loop - process blocks until completion or error
        while (!string.IsNullOrEmpty(currentBlockName))
        {
            // Check for cancellation requests before processing each block
            context.ThrowIfCancellationRequested();
            // Retrieve the block definition for the current block
            var blockDefinition = workflowDefinition.GetBlock(currentBlockName);
            if (blockDefinition == null)
            {
                throw new InvalidOperationException($"Block '{currentBlockName}' not found in workflow definition.");
            }
            // Execute the block using the helper method
            var nextBlockFromGuard = await ExecuteBlockAsync(workflowDefinition, currentBlockName, context, executionHistory).ConfigureAwait(false);
            if (nextBlockFromGuard != null)
            {
                currentBlockName = nextBlockFromGuard;
                continue;
            }
            // Get the last executed block info
            var lastInfo = executionHistory.Last();
            // Save execution checkpoint for potential resumption (configurable interval or on state change)
            var currentState = new Dictionary<string, object>(context.State);
            var (newCheckpointCounter, newPreviousState) = await SaveCheckpointIfNeededAsync(
                workflowDefinition, executionId, lastInfo.NextBlockName ?? string.Empty, currentState, executionHistory, checkpointInterval, checkpointCounter, previousState).ConfigureAwait(false);
            checkpointCounter = newCheckpointCounter;
            previousState = newPreviousState;
            // Determine the next block to execute based on execution result
            var nextBlockName = DetermineNextBlockName(blockDefinition, lastInfo);
            _logger?.LogBlockExecution(currentBlockName, lastInfo.CompletedAt - lastInfo.StartedAt, lastInfo.Status == ExecutionStatus.Success);
            // Handle wait conditions by pausing execution for specified duration
            await HandleWaitIfNeededAsync(lastInfo.Status, lastInfo.Output, workflowDefinition.Name, context.CancellationToken).ConfigureAwait(false);
            // Exit loop if no next block is specified (workflow completion)
            if (string.IsNullOrEmpty(nextBlockName))
            {
                _logger?.LogInformation("Workflow {WorkflowName} completed at block {BlockName}", workflowDefinition.Name, currentBlockName);
                break;
            }
            // Move to the next block in the workflow
            currentBlockName = nextBlockName;
        }
        // Return final workflow state
        return new Dictionary<string, object>(context.State);
    }
    /// <summary>
    /// Determines the next block to execute based on the current block's definition and execution result.
    /// This method implements the workflow's decision logic for block transitions.
    /// </summary>
    /// <param name="blockDefinition">The definition of the current block containing transition rules.</param>
    /// <param name="info">The execution info of the current block.</param>
    /// <returns>The name of the next block to execute, or null if workflow execution should end.</returns>
    private static string? DetermineNextBlockName(WorkflowBlockDefinition blockDefinition, BlockExecutionInfo info)
    {
        // If the execution result explicitly specifies the next block, use it
        if (!string.IsNullOrEmpty(info.NextBlockName))
        {
            return info.NextBlockName;
        }
        // Otherwise, use the block definition's conditional transitions based on success/failure
        return info.Status == ExecutionStatus.Success ? blockDefinition.NextBlockOnSuccess : blockDefinition.NextBlockOnFailure;
    }
    /// <summary>
    /// Gets the guards for a specific block, including global and block-specific guards.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition.</param>
    /// <param name="blockName">The name of the block.</param>
    /// <param name="isPreExecution">Whether to get pre-execution guards.</param>
    /// <returns>A list of IGuard instances.</returns>
    private List<IGuard> GetGuardsForBlock(WorkflowDefinition workflowDefinition, string blockName, bool isPreExecution)
    {
        var guards = new List<IGuard>();
        // Add global guards
        foreach (var globalGuardDef in workflowDefinition.GlobalGuards)
        {
            if ((isPreExecution && globalGuardDef.IsPreExecutionGuard) || (!isPreExecution && globalGuardDef.IsPostExecutionGuard))
            {
                var guard = _guardFactory.CreateGuard(globalGuardDef);
                if (guard != null)
                {
                    guards.Add(guard);
                }
            }
        }
        // Add block-specific guards
        if (workflowDefinition.BlockGuards.TryGetValue(blockName, out var blockGuardDefs))
        {
            foreach (var blockGuardDef in blockGuardDefs)
            {
                if ((isPreExecution && blockGuardDef.IsPreExecutionGuard) || (!isPreExecution && blockGuardDef.IsPostExecutionGuard))
                {
                    var guard = _guardFactory.CreateGuard(blockGuardDef);
                    if (guard != null)
                    {
                        guards.Add(guard);
                    }
                }
            }
        }
        return guards;
    }
    /// <summary>
    /// Evaluates guards for a block and returns the next block if transition is needed, or null to continue.
    /// Throws if guard fails without a failure block.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition.</param>
    /// <param name="blockName">The name of the block.</param>
    /// <param name="isPreExecution">Whether it's pre-execution.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="result">The execution result (for post-execution).</param>
    /// <returns>The next block name if transition, null if continue.</returns>
    private async Task<string?> EvaluateGuardsAndGetNextBlock(WorkflowDefinition workflowDefinition, string blockName, bool isPreExecution, ExecutionContext context, ExecutionResult? result = null)
    {
        var guards = GetGuardsForBlock(workflowDefinition, blockName, isPreExecution);
        if (!guards.Any())
        {
            return null;
        }

        var guardResults = isPreExecution
            ? await _guardManager.EvaluatePreExecutionGuardsAsync(guards, context).ConfigureAwait(false)
            : await _guardManager.EvaluatePostExecutionGuardsAsync(guards, context, result!).ConfigureAwait(false);
        var summary = _guardManager.CreateSummary(guardResults);
        if (summary.ShouldBlockExecution)
        {
            var mostSevere = summary.MostSevereFailure;
            if (mostSevere != null && !string.IsNullOrEmpty(mostSevere.FailureBlockName))
            {
                _logger?.LogWarning("Guard failed, transitioning to failure block {FailureBlock}", mostSevere.FailureBlockName);
                return mostSevere.FailureBlockName;
            }
            else
            {
                throw new InvalidOperationException($"Guard failed for block {blockName}: {mostSevere?.ErrorMessage}");
            }
        }
        return null;
    }
    /// <summary>
    /// Creates an execution checkpoint for the current state.
    /// </summary>
    private static ExecutionCheckpoint CreateCheckpoint(WorkflowDefinition workflowDefinition, Guid executionId, string currentBlockName, Dictionary<string, object> state, List<BlockExecutionInfo> history) => new ExecutionCheckpoint
    {
        WorkflowId = workflowDefinition.Id,
        ExecutionId = executionId,
        CurrentBlockName = currentBlockName,
        LastUpdatedUtc = DateTime.UtcNow,
        State = state,
        History = [.. history],
        RetryCount = 0,
        CorrelationId = executionId.ToString()
    };
    /// <summary>
    /// Saves a checkpoint if the interval is reached or state has changed.
    /// Returns updated checkpoint counter and previous state.
    /// </summary>
    private async Task<(int newCheckpointCounter, Dictionary<string, object> newPreviousState)> SaveCheckpointIfNeededAsync(
        WorkflowDefinition workflowDefinition,
        Guid executionId,
        string nextBlockName,
        Dictionary<string, object> currentState,
        List<BlockExecutionInfo> executionHistory,
        int checkpointInterval,
        int checkpointCounter,
        Dictionary<string, object> previousState)
    {
        var stateChanged = !previousState.SequenceEqual(currentState);
        var newCheckpointCounter = checkpointCounter + 1;
        if (newCheckpointCounter >= checkpointInterval || stateChanged)
        {
            var checkpoint = CreateCheckpoint(workflowDefinition, executionId, nextBlockName, currentState, executionHistory);
            await _workflowStore.SaveCheckpointAsync(checkpoint).ConfigureAwait(false);
            newCheckpointCounter = 0;
            previousState = currentState;
        }
        return (newCheckpointCounter, previousState);
    }
    /// <summary>
    /// Executes a single block and handles guards and transitions.
    /// Returns the next block name if guard transition, or null to continue with block result.
    /// </summary>
    private async Task<string?> ExecuteBlockAsync(WorkflowDefinition workflowDefinition, string currentBlockName, ExecutionContext context, List<BlockExecutionInfo> executionHistory)
    {
        // Retrieve the block definition for the current block
        var blockDefinition = workflowDefinition.GetBlock(currentBlockName);
        if (blockDefinition == null)
        {
            throw new InvalidOperationException($"Block '{currentBlockName}' not found in workflow definition.");
        }
        // Create the block instance using the factory
        var block = _blockFactory.CreateBlock(blockDefinition);
        if (block == null)
        {
            throw new InvalidOperationException($"Failed to create block '{currentBlockName}' of type '{blockDefinition.BlockType}'.");
        }
        // Update context with current block information and execute the block
        context.CurrentBlockName = currentBlockName;
        _logger?.LogDebug("Executing block {BlockName}", currentBlockName);
        // Evaluate pre-execution guards
        var nextBlockFromGuard = await EvaluateGuardsAndGetNextBlock(workflowDefinition, currentBlockName, true, context).ConfigureAwait(false);
        if (nextBlockFromGuard != null)
        {
            return nextBlockFromGuard; // Transition to failure block
        }
        var blockStartTime = DateTime.UtcNow;
        var result = await block.ExecuteAsync(context).ConfigureAwait(false);
        var blockEndTime = DateTime.UtcNow;
        // Evaluate post-execution guards
        nextBlockFromGuard = await EvaluateGuardsAndGetNextBlock(workflowDefinition, currentBlockName, false, context, result).ConfigureAwait(false);
        if (nextBlockFromGuard != null)
        {
            return nextBlockFromGuard; // Transition to failure block
        }
        // Record block execution details
        var blockExecutionInfo = new BlockExecutionInfo
        {
            BlockName = currentBlockName,
            BlockId = blockDefinition.BlockId,
            BlockType = blockDefinition.BlockType,
            StartedAt = blockStartTime,
            CompletedAt = blockEndTime,
            Status = result.Status,
            NextBlockName = result.NextBlockName,
            Output = result.Output
        };
        executionHistory.Add(blockExecutionInfo);
        // Notify execution monitor
        if (_monitor != null)
        {
            await _monitor.OnBlockExecutedAsync(blockExecutionInfo).ConfigureAwait(false);
        }
        // Handle wait conditions
        if (result.Status == ExecutionStatus.Wait && result.Output is TimeSpan waitDuration)
        {
            _logger?.LogInformation("Workflow {WorkflowName} waiting for {Duration}", workflowDefinition.Name, waitDuration);
            await Task.Delay(waitDuration, context.CancellationToken).ConfigureAwait(false);
        }
        return null; // Continue with result.NextBlockName
    }
    /// <summary>
    /// Handles wait conditions by pausing execution for the specified duration.
    /// </summary>
    private async Task HandleWaitIfNeededAsync(ExecutionStatus status, object? output, string workflowName, CancellationToken ct)
    {
        if (status == ExecutionStatus.Wait && output is TimeSpan waitDuration)
        {
            _logger?.LogInformation("Workflow {WorkflowName} waiting for {Duration}", workflowName, waitDuration);
            await Task.Delay(waitDuration, ct).ConfigureAwait(false);
        }
    }
    /// <summary>
    /// Notifies the execution monitor and logs workflow failure.
    /// </summary>
    private async Task NotifyWorkflowFailedAsync(WorkflowExecutionResult executionResult, Exception ex, IExecutionMonitor? monitor)
    {
        _logger?.LogError(ex, "Workflow {WorkflowId} failed after {Duration}", executionResult.WorkflowId, executionResult.Duration);
        if (monitor != null)
        {
            await monitor.OnWorkflowFailedAsync(executionResult, ex).ConfigureAwait(false);
        }
    }
}
