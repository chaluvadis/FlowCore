namespace FlowCore.CodeExecution;

/// <summary>
/// Enhanced workflow block that supports asynchronous code execution.
/// Extends the basic CodeBlock with async/await patterns and concurrent execution capabilities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AsyncCodeBlock.
/// </remarks>
/// <param name="executor">The code executor to use for execution.</param>
/// <param name="config">The code execution configuration.</param>
/// <param name="serviceProvider">The service provider for dependency injection.</param>
/// <param name="asyncConfig">Configuration for async execution behavior.</param>
/// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
/// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
/// <param name="logger">Optional logger for the block operations.</param>
public class AsyncCodeBlock(
    ICodeExecutor executor,
    CodeExecutionConfig config,
    IServiceProvider serviceProvider,
    AsyncExecutionConfig? asyncConfig = null,
    string nextBlockOnSuccess = "",
    string nextBlockOnFailure = "",
    ILogger? logger = null) : CodeBlock(executor, config, serviceProvider, nextBlockOnSuccess, nextBlockOnFailure, logger)
{
    private readonly IAsyncCodeExecutor? _asyncExecutor = executor as IAsyncCodeExecutor;
    private readonly AsyncExecutionConfig _asyncConfig = asyncConfig ?? AsyncExecutionConfig.Default;

    /// <summary>
    /// Gets the display name for this async code block.
    /// </summary>
    public override string DisplayName => $"AsyncCodeBlock({_config.Mode})";

    /// <summary>
    /// Gets the description of what this async code block does.
    /// </summary>
    public override string Description => $"Executes {_config.Language} code asynchronously using {_config.Mode} mode";

    /// <summary>
    /// Executes the core logic of the async code block.
    /// </summary>
    /// <param name="context">The execution context containing input data, state, and services.</param>
    /// <returns>An execution result indicating the outcome and next block to execute.</returns>
    protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
    {
        var executionStartTime = DateTime.UtcNow;

        try
        {
            LogInfo("Starting async code execution for block {BlockId}", BlockId);
            LogDebug("Async configuration: MaxParallelism={MaxParallelism}, Timeout={Timeout}",
                _asyncConfig.MaxDegreeOfParallelism, _asyncConfig.DefaultTimeout);

            // Check if we have an async executor and if the code supports async execution
            if (_asyncExecutor != null && _asyncExecutor.SupportsAsyncExecution(_config))
            {
                return await ExecuteAsyncCodeAsync(context, executionStartTime);
            }
            else
            {
                // Fall back to regular execution
                LogDebug("Falling back to synchronous execution");
                return await base.ExecuteBlockAsync(context);
            }
        }
        catch (OperationCanceledException)
        {
            LogWarning("Async code execution was cancelled for block {BlockId}", BlockId);
            throw;
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - executionStartTime;
            LogError(ex, "Unexpected error during async code execution for block {BlockId} after {ExecutionTime}", BlockId, executionTime);
            return ExecutionResult.Failure(NextBlockOnFailure, null, ex);
        }
    }

    private async Task<ExecutionResult> ExecuteAsyncCodeAsync(ExecutionContext context, DateTime executionStartTime)
    {
        try
        {
            // Create enhanced async execution context
            using var asyncContext = new AsyncCodeExecutionContext(context, _config, _serviceProvider, _asyncConfig);

            // Execute the code using the async executor
            var executionResult = await _asyncExecutor!.ExecuteAsyncCodeAsync(asyncContext, context.CancellationToken);

            var executionTime = DateTime.UtcNow - executionStartTime;

            if (executionResult.Success)
            {
                LogInfo("Async code execution completed successfully in {ExecutionTime}. Async operations: {AsyncOps}, Peak concurrency: {PeakConcurrency}. Next block: {NextBlock}",
                    executionTime, executionResult.AsyncOperations.Count, executionResult.ActualDegreeOfParallelism, NextBlockOnSuccess);

                // Log performance metrics if detailed logging is enabled
                if (_asyncConfig.EnableDetailedLogging)
                {
                    LogDebug("Async performance: Efficiency={Efficiency:P2}, Avg operation time={AvgTime}, Total async wait={AsyncWait}",
                        executionResult.PerformanceMetrics.EfficiencyRatio,
                        executionResult.PerformanceMetrics.AverageOperationTime,
                        executionResult.TotalAsyncWaitTime);
                }

                // Create metadata for the execution result
                var metadata = new Models.ExecutionMetadata(ExecutionStatus.Success, executionStartTime);
                metadata.MarkCompleted();
                metadata.AddInfo($"Async code execution completed in {executionTime.TotalMilliseconds}ms with {executionResult.AsyncOperations.Count} async operations");

                return ExecutionResult.Success(NextBlockOnSuccess, executionResult.Output);
            }
            else
            {
                LogWarning("Async code execution failed in {ExecutionTime}: {ErrorMessage}. Next block: {NextBlock}",
                    executionTime, executionResult.ErrorMessage, NextBlockOnFailure);

                return ExecutionResult.Failure(NextBlockOnFailure, null, executionResult.Exception ?? new Exception(executionResult.ErrorMessage));
            }
        }
        catch (TimeoutException tex)
        {
            var executionTime = DateTime.UtcNow - executionStartTime;
            LogWarning("Async code execution timed out after {ExecutionTime} for block {BlockId}", executionTime, BlockId);
            return ExecutionResult.Failure(NextBlockOnFailure, null, tex);
        }
    }

    /// <summary>
    /// Validates whether this async block can execute with the given context.
    /// </summary>
    /// <param name="context">The execution context to validate against.</param>
    /// <returns>A task representing whether the block can execute.</returns>
    public override async Task<bool> CanExecuteAsync(ExecutionContext context)
    {
        try
        {
            LogDebug("Validating async execution capability for block {BlockId}", BlockId);

            // First, check the base validation
            if (!await base.CanExecuteAsync(context))
            {
                return false;
            }

            // Additional async-specific validation
            if (_asyncExecutor != null)
            {
                // Validate async execution support
                if (!_asyncExecutor.SupportsAsyncExecution(_config))
                {
                    LogDebug("Code does not contain async patterns, will use synchronous execution");
                    // This is not a failure - we can still execute synchronously
                }

                // Validate async configuration
                if (_asyncConfig.MaxDegreeOfParallelism < 1)
                {
                    LogWarning("Invalid MaxDegreeOfParallelism ({MaxParallelism}) for async block {BlockId}",
                        _asyncConfig.MaxDegreeOfParallelism, BlockId);
                    return false;
                }

                if (_asyncConfig.DefaultTimeout <= TimeSpan.Zero)
                {
                    LogWarning("Invalid timeout ({Timeout}) for async block {BlockId}",
                        _asyncConfig.DefaultTimeout, BlockId);
                    return false;
                }
            }

            LogDebug("Async pre-execution validation passed for block {BlockId}", BlockId);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during async pre-execution validation for block {BlockId}", BlockId);
            return false;
        }
    }

    /// <summary>
    /// Performs cleanup after async block execution.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="result">The result of the block execution.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    public override async Task CleanupAsync(ExecutionContext context, ExecutionResult result)
    {
        try
        {
            LogDebug("Performing async cleanup for block {BlockId}", BlockId);

            // Clean up async-specific temporary state
            var asyncTempKeys = context.State.Keys
                .Where(k => k.StartsWith("async_temp_") || k.StartsWith("_async_temp") || k.StartsWith("temp_result_"))
                .ToList();

            foreach (var key in asyncTempKeys)
            {
                context.RemoveState(key);
            }

            if (asyncTempKeys.Count != 0)
            {
                LogDebug("Cleared {AsyncTempKeyCount} async temporary state keys", asyncTempKeys.Count);
            }

            LogDebug("Async cleanup completed for block {BlockId}", BlockId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during async cleanup for block {BlockId}", BlockId);
        }
        finally
        {
            await base.CleanupAsync(context, result);
        }
    }

    /// <summary>
    /// Creates a new AsyncCodeBlock instance from configuration.
    /// </summary>
    /// <param name="config">The code execution configuration.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="asyncConfig">Configuration for async execution behavior.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger for the block.</param>
    /// <returns>A new AsyncCodeBlock instance.</returns>
    public static new AsyncCodeBlock Create(
        CodeExecutionConfig config,
        IServiceProvider serviceProvider,
        AsyncExecutionConfig? asyncConfig = null,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null)
    {
        // Resolve the appropriate executor based on the configuration mode
        var executor = ResolveAsyncExecutor(config, serviceProvider, logger);

        return new AsyncCodeBlock(executor, config, serviceProvider, asyncConfig, nextBlockOnSuccess, nextBlockOnFailure, logger);
    }

    private static ICodeExecutor ResolveAsyncExecutor(CodeExecutionConfig config, IServiceProvider serviceProvider, ILogger? logger) => config.Mode switch
    {
        CodeExecutionMode.Inline => new AsyncInlineCodeExecutor(
            CodeSecurityConfig.Create(config.AllowedNamespaces, config.AllowedTypes, config.BlockedNamespaces),
            logger),

        CodeExecutionMode.Assembly => new AssemblyCodeExecutor(
            CodeSecurityConfig.Create(config.AllowedNamespaces, config.AllowedTypes, config.BlockedNamespaces),
            logger),

        _ => throw new NotSupportedException($"Code execution mode {config.Mode} is not supported")
    };
}