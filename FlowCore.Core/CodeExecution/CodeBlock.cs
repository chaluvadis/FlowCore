namespace FlowCore.CodeExecution;

/// <summary>
/// Workflow block that executes configurable code.
/// Supports both inline C# code execution and pre-compiled assembly execution.
/// Provides seamless integration with workflow state and data flow.
/// </summary>
public class CodeBlock : WorkflowBlockBase
{
    private readonly ICodeExecutor _executor;
    protected readonly CodeExecutionConfig _config;
    protected readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Gets or sets the name of the next block to execute on successful completion.
    /// </summary>
    public override string NextBlockOnSuccess { get; protected set; }

    /// <summary>
    /// Gets or sets the name of the next block to execute on failure.
    /// </summary>
    public override string NextBlockOnFailure { get; protected set; }

    /// <summary>
    /// Gets the unique identifier for this code block.
    /// </summary>
    public override string BlockId => $"{GetType().Name}_{_config.GetHashCode():X8}";

    /// <summary>
    /// Gets the display name for this code block.
    /// </summary>
    public override string DisplayName => $"CodeBlock({_config.Mode})";

    /// <summary>
    /// Gets the version of this code block.
    /// </summary>
    public override string Version => "1.0.0";

    /// <summary>
    /// Gets the description of what this code block does.
    /// </summary>
    public override string Description => $"Executes {_config.Language} code using {_config.Mode} mode";

    /// <summary>
    /// Initializes a new instance of the CodeBlock.
    /// </summary>
    /// <param name="executor">The code executor to use for execution.</param>
    /// <param name="config">The code execution configuration.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger for the block operations.</param>
    public CodeBlock(
        ICodeExecutor executor,
        CodeExecutionConfig config,
        IServiceProvider serviceProvider,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null) : base(logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        NextBlockOnSuccess = nextBlockOnSuccess;
        NextBlockOnFailure = nextBlockOnFailure;

        LogInfo("CodeBlock initialized with mode {Mode}, language {Language}", _config.Mode, _config.Language);
    }

    /// <summary>
    /// Executes the core logic of the code block.
    /// </summary>
    /// <param name="context">The execution context containing input data, state, and services.</param>
    /// <returns>An execution result indicating the outcome and next block to execute.</returns>
    protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
    {
        var executionStartTime = DateTime.UtcNow;

        try
        {
            LogInfo("Starting code execution for block {BlockId}", BlockId);
            LogDebug("Code configuration: Mode={Mode}, Language={Language}, Timeout={Timeout}",
                _config.Mode, _config.Language, _config.Timeout);

            // Validate that the executor can handle this configuration
            if (!_executor.CanExecute(_config))
            {
                var error = $"Executor {_executor.ExecutorType} cannot handle configuration mode {_config.Mode}";
                LogError(new InvalidOperationException(error), "Executor capability validation failed");
                return ExecutionResult.Failure(NextBlockOnFailure, null, new InvalidOperationException(error));
            }

            // Create code execution context with access to workflow state
            var codeContext = new CodeExecutionContext(context, _config, _serviceProvider);

            // Execute the code using the configured executor
            var executionResult = await _executor.ExecuteAsync(codeContext, context.CancellationToken).ConfigureAwait(false);

            var executionTime = DateTime.UtcNow - executionStartTime;

            if (executionResult.Success)
            {
                LogInfo("Code execution completed successfully in {ExecutionTime}. Next block: {NextBlock}",
                    executionTime, NextBlockOnSuccess);

                // Create metadata for the execution result
                var metadata = new Models.ExecutionMetadata(ExecutionStatus.Success, executionStartTime);
                metadata.MarkCompleted();
                metadata.AddInfo($"Code execution completed successfully in {executionTime.TotalMilliseconds}ms");

                return ExecutionResult.Success(NextBlockOnSuccess, executionResult.Output);
            }
            else
            {
                var errorMessage = executionResult.ErrorMessage ?? "Unknown Error";
                LogWarning("Code execution failed in {ExecutionTime}: {ErrorMessage}. Next block: {NextBlock}",
                    executionTime, errorMessage, NextBlockOnFailure);

                return ExecutionResult.Failure(NextBlockOnFailure, null, executionResult.Exception ?? new WorkflowException(errorMessage));
            }
        }
        catch (OperationCanceledException)
        {
            LogWarning("Code execution was cancelled for block {BlockId}", BlockId);
            throw;
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - executionStartTime;
            LogError(ex, "Unexpected error during code execution for block {BlockId} after {ExecutionTime}", BlockId, executionTime);

            return ExecutionResult.Failure(NextBlockOnFailure, null, ex);
        }
    }

    /// <summary>
    /// Validates whether this block can execute with the given context.
    /// </summary>
    /// <param name="context">The execution context to validate against.</param>
    /// <returns>A task representing whether the block can execute.</returns>
    public override async Task<bool> CanExecuteAsync(ExecutionContext context)
    {
        try
        {
            LogDebug("Validating execution capability for block {BlockId}", BlockId);

            // Check if required state keys are present
            if (_config.ValidateCode)
            {
                foreach (var requiredKey in _config.Parameters.Keys.Where(k => k.StartsWith("Required:")))
                {
                    var actualKey = requiredKey.Substring("Required:".Length);
                    if (!context.State.ContainsKey(actualKey))
                    {
                        LogWarning("Required state key '{Key}' not found for block {BlockId}", actualKey, BlockId);
                        return false;
                    }
                }
            }

            // Validate security configuration
            var securityValidation = _executor.ValidateExecutionSafety(_config);
            if (!securityValidation.IsValid)
            {
                LogWarning("Security validation failed for block {BlockId}: {Errors}", BlockId, string.Join(", ", securityValidation.Errors));
                return false;
            }

            // Validate code is not empty for inline mode
            if (_config.Mode == CodeExecutionMode.Inline && string.IsNullOrEmpty(_config.Code))
            {
                LogWarning("No code provided for inline execution in block {BlockId}", BlockId);
                return false;
            }

            // Validate assembly file exists for assembly mode
            if (_config.Mode == CodeExecutionMode.Assembly && !File.Exists(_config.AssemblyPath))
            {
                LogWarning("Assembly file not found: {AssemblyPath} for block {BlockId}", _config.AssemblyPath, BlockId);
                return false;
            }

            LogDebug("Pre-execution validation passed for block {BlockId}", BlockId);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during pre-execution validation for block {BlockId}", BlockId);
            return false;
        }
    }

    /// <summary>
    /// Performs cleanup after block execution.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="result">The result of the block execution.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    public override async Task CleanupAsync(ExecutionContext context, ExecutionResult result)
    {
        try
        {
            LogDebug("Performing cleanup for block {BlockId}", BlockId);

            // Clear temporary state (always enabled for code blocks for security)
            var tempKeys = context.State.Keys.Where(k => k.StartsWith("temp_") || k.StartsWith("_temp")).ToList();
            foreach (var key in tempKeys)
            {
                context.RemoveState(key);
            }
            if (tempKeys.Count != 0)
            {
                LogDebug("Cleared {TempKeyCount} temporary state keys", tempKeys.Count);
            }

            // Log cleanup completion
            LogDebug("Cleanup completed for block {BlockId}", BlockId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during cleanup for block {BlockId}", BlockId);
        }
        finally
        {
            await base.CleanupAsync(context, result).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a new CodeBlock instance from configuration.
    /// </summary>
    /// <param name="config">The code execution configuration.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger for the block.</param>
    /// <returns>A new CodeBlock instance.</returns>
    public static CodeBlock Create(
        CodeExecutionConfig config,
        IServiceProvider serviceProvider,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null)
    {
        // Resolve the appropriate executor based on the configuration mode
        var executor = ResolveExecutor(config, logger);

        return new CodeBlock(executor, config, serviceProvider, nextBlockOnSuccess, nextBlockOnFailure, logger);
    }

    private static ICodeExecutor ResolveExecutor(CodeExecutionConfig config, ILogger? logger) => config.Mode switch
    {
        CodeExecutionMode.Inline => new InlineCodeExecutor(
            CodeSecurityConfig.Create(config.AllowedNamespaces, config.AllowedTypes, config.BlockedNamespaces),
            logger),

        CodeExecutionMode.Assembly => new AssemblyCodeExecutor(
            CodeSecurityConfig.Create(config.AllowedNamespaces, config.AllowedTypes, config.BlockedNamespaces),
            logger),

        _ => throw new NotSupportedException($"Code execution mode {config.Mode} is not supported")
    };
}
