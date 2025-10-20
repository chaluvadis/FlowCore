namespace LinkedListWorkflowEngine.Core;
public class WorkflowEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowBlockFactory _blockFactory;
    private readonly IStateManager? _stateManager;
    private readonly WorkflowStatePersistenceService? _persistenceService;
    private readonly ILogger<WorkflowEngine>? _logger;
    private readonly StateManagerConfig _stateManagerConfig;
    private readonly ErrorHandler _errorHandler;
    public WorkflowEngine(
        IServiceProvider serviceProvider,
        ILogger<WorkflowEngine>? logger = null,
        IWorkflowBlockFactory? blockFactory = null,
        IStateManager? stateManager = null,
        StateManagerConfig? stateManagerConfig = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
        _blockFactory = blockFactory ?? new WorkflowBlockFactory(serviceProvider);
        _stateManager = stateManager;
        _stateManagerConfig = stateManagerConfig ?? new StateManagerConfig();
        _persistenceService = _stateManager != null ? new WorkflowStatePersistenceService(_stateManager) : null;
        _errorHandler = new ErrorHandler(_logger as ILogger<ErrorHandler> ?? new LoggerFactory().CreateLogger<ErrorHandler>());
    }
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition workflowDefinition,
        object input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        ArgumentNullException.ThrowIfNull(input);
        // Validate workflow definition
        if (!workflowDefinition.IsValid())
        {
            throw new InvalidOperationException($"Workflow definition '{workflowDefinition.Id}' is not valid.");
        }
        _logger?.LogInformation("Starting execution of workflow {WorkflowId} v{Version}",
            workflowDefinition.Id, workflowDefinition.Version);
        // Create execution context
        var context = new ExecutionContext(
            input,
            _serviceProvider,
            cancellationToken,
            workflowDefinition.Name);
        var executionId = Guid.NewGuid();
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
            // Execute the workflow
            var finalState = await ExecuteWorkflowInternalAsync(workflowDefinition, context, executionId);
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Completed;
            executionResult.FinalState = finalState;
            executionResult.Succeeded = true;
            // Save final checkpoint if persistence is enabled
            if (_persistenceService != null)
            {
                await _persistenceService.SaveCheckpointAsync(
                    workflowDefinition.Id,
                    executionId,
                    context,
                    WorkflowStatus.Completed,
                    _stateManagerConfig.CheckpointFrequency);
            }
            _logger?.LogInformation("Workflow {WorkflowId} completed successfully in {Duration}",
                workflowDefinition.Id, executionResult.Duration);
            return executionResult;
        }
        catch (OperationCanceledException)
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Cancelled;
            executionResult.Succeeded = false;
            _logger?.LogWarning("Workflow {WorkflowId} was cancelled after {Duration}",
                workflowDefinition.Id, executionResult.Duration);
            throw;
        }
        catch (Exception ex)
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Failed;
            executionResult.Succeeded = false;
            executionResult.Error = ex;
            // Handle the error using the error handling framework
            var blockName = context.CurrentBlockName ?? "Unknown";
            var errorHandlingResult = await _errorHandler.HandleErrorAsync(
                ex,
                context,
                blockName,
                workflowDefinition.ExecutionConfig.RetryPolicy);
            _logger?.LogError(ex, "Workflow {WorkflowId} failed after {Duration} with error handling: {ErrorHandlingAction}",
                workflowDefinition.Id, executionResult.Duration, errorHandlingResult.Action);
            // Re-throw if we should fail, or handle according to error strategy
            if (errorHandlingResult.Action == ErrorHandlingAction.Fail)
            {
                throw;
            }
            else if (errorHandlingResult.Action == ErrorHandlingAction.Skip)
            {
                // Continue to next block if possible
                _logger?.LogWarning("Skipping failed block {BlockName} and continuing workflow", blockName);
                // For now, we'll still mark as failed since we can't easily continue from a failed block
                // In a more sophisticated implementation, we could have error transition paths
                throw;
            }
            // If we reach here, return the failed execution result
            return executionResult;
        }
    }
    /// <summary>
    /// Internal method that performs the actual workflow execution.
    /// </summary>
    private async Task<IDictionary<string, object>> ExecuteWorkflowInternalAsync(
        WorkflowDefinition workflowDefinition,
        ExecutionContext context,
        Guid executionId)
    {
        var currentBlockName = workflowDefinition.StartBlockName;
        var executionHistory = new List<BlockExecutionInfo>();
        while (!string.IsNullOrEmpty(currentBlockName))
        {
            // Check for cancellation
            context.ThrowIfCancellationRequested();
            // Get the current block definition
            var blockDefinition = workflowDefinition.GetBlock(currentBlockName);
            if (blockDefinition == null)
            {
                throw new InvalidOperationException($"Block '{currentBlockName}' not found in workflow definition.");
            }
            // Create the block instance
            var block = _blockFactory.CreateBlock(blockDefinition);
            if (block == null)
            {
                throw new InvalidOperationException($"Failed to create block '{currentBlockName}' of type '{blockDefinition.BlockType}'.");
            }
            // Update context with current block information
            context.CurrentBlockName = currentBlockName;
            _logger?.LogDebug("Executing block {BlockName} ({BlockId})",
                currentBlockName, blockDefinition.BlockId);
            // Execute the block
            var blockStartTime = DateTime.UtcNow;
            var result = await block.ExecuteAsync(context);
            var blockEndTime = DateTime.UtcNow;
            // Save checkpoint after block execution if persistence is enabled
            if (_persistenceService != null && _stateManagerConfig.CheckpointFrequency == CheckpointFrequency.AfterEachBlock)
            {
                await _persistenceService.SaveCheckpointAsync(
                    workflowDefinition.Id,
                    executionId,
                    context,
                    WorkflowStatus.Running,
                    _stateManagerConfig.CheckpointFrequency);
            }
            // Record execution info
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
            // Determine the next block based on the result
            var nextBlockName = DetermineNextBlockName(blockDefinition, result);
            _logger?.LogDebug("Block {BlockName} completed with status {Status}, next block: {NextBlock}",
                currentBlockName, result.Status, nextBlockName ?? "END");
            // Handle special execution results
            if (result.Status == ExecutionStatus.Wait && result.Output is TimeSpan waitDuration)
            {
                _logger?.LogInformation("Workflow {WorkflowName} waiting for {Duration} before continuing",
                    workflowDefinition.Name, waitDuration);
                await Task.Delay(waitDuration, context.CancellationToken);
            }
            // Check if workflow should end
            if (string.IsNullOrEmpty(nextBlockName))
            {
                _logger?.LogInformation("Workflow {WorkflowName} reached end state at block {BlockName}",
                    workflowDefinition.Name, currentBlockName);
                break;
            }
            // Move to next block
            currentBlockName = nextBlockName;
        }
        return new Dictionary<string, object>(context.State);
    }
    /// <summary>
    /// Determines the next block name based on the block definition and execution result.
    /// </summary>
    private static string? DetermineNextBlockName(WorkflowBlockDefinition blockDefinition, ExecutionResult result)
    {
        // If the result specifies a next block, use it
        if (!string.IsNullOrEmpty(result.NextBlockName))
        {
            return result.NextBlockName;
        }
        // Otherwise, use the block's default transitions
        return result.IsSuccess ? blockDefinition.NextBlockOnSuccess : blockDefinition.NextBlockOnFailure;
    }
    /// <summary>
    /// Resumes a workflow execution from a saved checkpoint.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to resume.</param>
    /// <param name="executionId">The execution ID to resume.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The workflow execution result.</returns>
    public async Task<WorkflowExecutionResult> ResumeFromCheckpointAsync(
        WorkflowDefinition workflowDefinition,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        if (_persistenceService == null || _stateManager == null)
        {
            throw new InvalidOperationException("State persistence is not configured for this workflow engine");
        }
        // Load the latest checkpoint
        var context = await _persistenceService.LoadLatestCheckpointAsync(
            workflowDefinition.Id,
            executionId,
            _serviceProvider,
            cancellationToken);
        if (context == null)
        {
            throw new InvalidOperationException($"No checkpoint found for workflow {workflowDefinition.Id}, execution {executionId}");
        }
        _logger?.LogInformation("Resuming workflow {WorkflowId} from checkpoint, execution {ExecutionId}",
            workflowDefinition.Id, executionId);
        // Continue execution from the loaded state
        var finalState = await ExecuteWorkflowInternalAsync(workflowDefinition, context, executionId);
        var executionResult = new WorkflowExecutionResult
        {
            WorkflowId = workflowDefinition.Id,
            WorkflowVersion = workflowDefinition.Version,
            ExecutionId = executionId,
            StartedAt = DateTime.UtcNow, // This will be updated when we load the original start time from metadata
            CompletedAt = DateTime.UtcNow,
            Status = WorkflowStatus.Completed,
            FinalState = finalState,
            Succeeded = true
        };
        _logger?.LogInformation("Workflow {WorkflowId} resumed and completed successfully", workflowDefinition.Id);
        return executionResult;
    }
    /// <summary>
    /// Suspends a running workflow execution.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A task representing the suspend operation.</returns>
    public async Task SuspendWorkflowAsync(string workflowId, Guid executionId, ExecutionContext context)
    {
        if (_persistenceService == null)
        {
            throw new InvalidOperationException("State persistence is not configured for this workflow engine");
        }
        await _persistenceService.SaveCheckpointAsync(
            workflowId,
            executionId,
            context,
            WorkflowStatus.Suspended,
            CheckpointFrequency.AfterEachBlock);
        _logger?.LogInformation("Workflow {WorkflowId}, execution {ExecutionId} suspended", workflowId, executionId);
    }
    /// <summary>
    /// Gets the current state manager configuration.
    /// </summary>
    public StateManagerConfig GetStateManagerConfig() => _stateManagerConfig;
}