namespace FlowCore.Common;

/// <summary>
/// Exception thrown for workflow-related errors.
/// </summary>
public class WorkflowException : Exception
{
    public WorkflowException(string message) : base(message) { }
    public WorkflowException(string message, Exception innerException) : base(message, innerException) { }
}
/// <summary>
/// Basic workflow blocks for common operations.
/// </summary>
public static class BasicBlocks
{
    /// <summary>
    /// A workflow block that logs a message.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the LogBlock class.
    /// </remarks>
    /// <param name="message">The message to log.</param>
    /// <param name="logLevel">The log level to use.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger.</param>
    public class LogBlock(
        string message,
        LogLevel logLevel = LogLevel.Information,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null) : WorkflowBlockBase(logger)
    {

        /// <summary>
        /// Gets or sets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;

        /// <summary>
        /// Gets or sets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            // Log the message based on the specified level
            switch (logLevel)
            {
                case LogLevel.Trace:
                    LogDebug(message);
                    break;
                case LogLevel.Debug:
                    LogDebug(message);
                    break;
                case LogLevel.Information:
                    LogInfo(message);
                    break;
                case LogLevel.Warning:
                    LogWarning(message);
                    break;
                case LogLevel.Error:
                    LogError(new WorkflowException(message), message);
                    break;
                case LogLevel.Critical:
                    LogError(new WorkflowException(message), message);
                    break;
                default:
                    LogInfo(message);
                    break;
            }

            // Also store the message in the context state
            context.SetState("LastLogMessage", message);
            context.SetState("LastLogLevel", logLevel.ToString());

            await Task.CompletedTask.ConfigureAwait(false);
            return ExecutionResult.Success(NextBlockOnSuccess);
        }
    }

    /// <summary>
    /// A workflow block that waits for a specified duration.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the WaitBlock class.
    /// </remarks>
    /// <param name="duration">The duration to wait.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger.</param>
    public class WaitBlock(
        TimeSpan duration,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null) : WorkflowBlockBase(logger)
    {

        /// <summary>
        /// Gets or sets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;

        /// <summary>
        /// Gets or sets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            LogInfo($"Waiting for {duration.TotalSeconds} seconds");

            // Store the wait duration in context state
            context.SetState("WaitDuration", duration);

            await Task.Delay(duration, context.CancellationToken).ConfigureAwait(false);

            return ExecutionResult.Success(NextBlockOnSuccess);
        }
    }

    /// <summary>
    /// A workflow block that sets a value in the execution context state.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the SetStateBlock class.
    /// </remarks>
    /// <param name="key">The key to set in the context state.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger.</param>
    public class SetStateBlock(
        string key,
        object value,
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null) : WorkflowBlockBase(logger)
    {

        /// <summary>
        /// Gets or sets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;

        /// <summary>
        /// Gets or sets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            LogInfo($"Setting state key '{key}' to value '{value}'");

            context.SetState(key, value);

            await Task.CompletedTask.ConfigureAwait(false);
            return ExecutionResult.Success(NextBlockOnSuccess);
        }
    }

    /// <summary>
    /// A workflow block that conditionally executes based on a predicate.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the ConditionalBlock class.
    /// </remarks>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="nextBlockOnConditionMet">The next block if condition is true.</param>
    /// <param name="nextBlockOnConditionNotMet">The next block if condition is false.</param>
    /// <param name="logger">Optional logger.</param>
    public class ConditionalBlock(
        Func<ExecutionContext, bool> condition,
        string nextBlockOnConditionMet = "",
        string nextBlockOnConditionNotMet = "",
        ILogger? logger = null) : WorkflowBlockBase(logger)
    {
        private readonly Func<ExecutionContext, bool> _condition
            = condition ?? throw new ArgumentNullException(nameof(condition));

        /// <summary>
        /// Gets or sets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnConditionMet;

        /// <summary>
        /// Gets or sets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; protected set; } = nextBlockOnConditionNotMet;

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            var conditionResult = _condition(context);

            LogInfo($"Condition evaluated to: {conditionResult}");

            var nextBlock = conditionResult ? NextBlockOnSuccess : NextBlockOnFailure;

            await Task.CompletedTask.ConfigureAwait(false);
            return ExecutionResult.Success(nextBlock);
        }
    }

    /// <summary>
    /// A workflow block that always fails for testing error handling.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the FailBlock class.
    /// </remarks>
    /// <param name="errorMessage">The error message to fail with.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
    /// <param name="logger">Optional logger.</param>
    public class FailBlock(
        string errorMessage = "Intentional failure for testing",
        string nextBlockOnSuccess = "",
        string nextBlockOnFailure = "",
        ILogger? logger = null) : WorkflowBlockBase(logger)
    {

        /// <summary>
        /// Gets or sets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;

        /// <summary>
        /// Gets or sets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            LogError(new WorkflowException(errorMessage), "Intentional failure");

            await Task.CompletedTask.ConfigureAwait(false);
            return ExecutionResult.Failure(NextBlockOnFailure, null, new WorkflowException(errorMessage));
        }
    }
}
