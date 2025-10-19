namespace LinkedListWorkflowEngine.Core;
public class WorkflowEngine(
    IServiceProvider serviceProvider,
    ILogger<WorkflowEngine>? logger = null,
    IWorkflowBlockFactory? blockFactory = null)
{
    private readonly IServiceProvider _serviceProvider
        = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IWorkflowBlockFactory _blockFactory
        = blockFactory ?? new WorkflowBlockFactory(serviceProvider);

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

        logger?.LogInformation("Starting execution of workflow {WorkflowId} v{Version}",
            workflowDefinition.Id, workflowDefinition.Version);

        // Create execution context
        var context = new ExecutionContext(
            input,
            _serviceProvider,
            cancellationToken,
            workflowDefinition.Name);

        var executionResult = new WorkflowExecutionResult
        {
            WorkflowId = workflowDefinition.Id,
            WorkflowVersion = workflowDefinition.Version,
            StartedAt = DateTime.UtcNow,
            Status = WorkflowStatus.Running
        };

        try
        {
            // Execute the workflow
            var finalState = await ExecuteWorkflowInternalAsync(workflowDefinition, context);

            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Completed;
            executionResult.FinalState = finalState;
            executionResult.Succeeded = true;

            logger?.LogInformation("Workflow {WorkflowId} completed successfully in {Duration}",
                workflowDefinition.Id, executionResult.Duration);

            return executionResult;
        }
        catch (OperationCanceledException)
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Cancelled;
            executionResult.Succeeded = false;

            logger?.LogWarning("Workflow {WorkflowId} was cancelled after {Duration}",
                workflowDefinition.Id, executionResult.Duration);

            throw;
        }
        catch (Exception ex)
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Status = WorkflowStatus.Failed;
            executionResult.Succeeded = false;
            executionResult.Error = ex;

            logger?.LogError(ex, "Workflow {WorkflowId} failed after {Duration}",
                workflowDefinition.Id, executionResult.Duration);

            throw;
        }
    }

    /// <summary>
    /// Internal method that performs the actual workflow execution.
    /// </summary>
    private async Task<IDictionary<string, object>> ExecuteWorkflowInternalAsync(
        WorkflowDefinition workflowDefinition,
        ExecutionContext context)
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

            logger?.LogDebug("Executing block {BlockName} ({BlockId})",
                currentBlockName, blockDefinition.BlockId);

            // Execute the block
            var blockStartTime = DateTime.UtcNow;
            var result = await block.ExecuteAsync(context);
            var blockEndTime = DateTime.UtcNow;

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

            logger?.LogDebug("Block {BlockName} completed with status {Status}, next block: {NextBlock}",
                currentBlockName, result.Status, nextBlockName ?? "END");

            // Handle special execution results
            if (result.Status == ExecutionStatus.Wait && result.Output is TimeSpan waitDuration)
            {
                logger?.LogInformation("Workflow {WorkflowName} waiting for {Duration} before continuing",
                    workflowDefinition.Name, waitDuration);

                await Task.Delay(waitDuration, context.CancellationToken);
            }

            // Check if workflow should end
            if (string.IsNullOrEmpty(nextBlockName))
            {
                logger?.LogInformation("Workflow {WorkflowName} reached end state at block {BlockName}",
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
}