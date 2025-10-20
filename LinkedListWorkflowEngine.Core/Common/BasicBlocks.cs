namespace LinkedListWorkflowEngine.Core.Common;

/// <summary>
/// Basic workflow blocks for common operations.
/// </summary>
public static class BasicBlocks
{
    /// <summary>
    /// A workflow block that logs a message.
    /// </summary>
    public class LogBlock : WorkflowBlockBase
    {
        private readonly string _message;
        private readonly LogLevel _logLevel;

        /// <summary>
        /// Gets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; }

        /// <summary>
        /// Gets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; }

        /// <summary>
        /// Initializes a new instance of the LogBlock class.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="logLevel">The log level to use.</param>
        /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
        /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
        /// <param name="logger">Optional logger.</param>
        public LogBlock(
            string message,
            LogLevel logLevel = LogLevel.Information,
            string nextBlockOnSuccess = "",
            string nextBlockOnFailure = "",
            ILogger? logger = null) : base(logger)
        {
            _message = message;
            _logLevel = logLevel;
            NextBlockOnSuccess = nextBlockOnSuccess;
            NextBlockOnFailure = nextBlockOnFailure;
        }

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            // Log the message based on the specified level
            switch (_logLevel)
            {
                case LogLevel.Trace:
                    LogDebug(_message);
                    break;
                case LogLevel.Debug:
                    LogDebug(_message);
                    break;
                case LogLevel.Information:
                    LogInfo(_message);
                    break;
                case LogLevel.Warning:
                    LogWarning(_message);
                    break;
                case LogLevel.Error:
                    LogError(new Exception(_message), _message);
                    break;
                case LogLevel.Critical:
                    LogError(new Exception(_message), _message);
                    break;
                default:
                    LogInfo(_message);
                    break;
            }

            // Also store the message in the context state
            context.SetState("LastLogMessage", _message);
            context.SetState("LastLogLevel", _logLevel.ToString());

            await Task.CompletedTask;
            return ExecutionResult.Success(NextBlockOnSuccess);
        }
    }

    /// <summary>
    /// A workflow block that waits for a specified duration.
    /// </summary>
    public class WaitBlock : WorkflowBlockBase
    {
        private readonly TimeSpan _duration;

        /// <summary>
        /// Gets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; }

        /// <summary>
        /// Gets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; }

        /// <summary>
        /// Initializes a new instance of the WaitBlock class.
        /// </summary>
        /// <param name="duration">The duration to wait.</param>
        /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
        /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
        /// <param name="logger">Optional logger.</param>
        public WaitBlock(
            TimeSpan duration,
            string nextBlockOnSuccess = "",
            string nextBlockOnFailure = "",
            ILogger? logger = null) : base(logger)
        {
            _duration = duration;
            NextBlockOnSuccess = nextBlockOnSuccess;
            NextBlockOnFailure = nextBlockOnFailure;
        }

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            LogInfo($"Waiting for {_duration.TotalSeconds} seconds");

            // Store the wait duration in context state
            context.SetState("WaitDuration", _duration);

            await Task.Delay(_duration, context.CancellationToken);

            return ExecutionResult.Success(NextBlockOnSuccess);
        }
    }

    /// <summary>
    /// A workflow block that sets a value in the execution context state.
    /// </summary>
    public class SetStateBlock : WorkflowBlockBase
    {
        private readonly string _key;
        private readonly object _value;

        /// <summary>
        /// Gets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; }

        /// <summary>
        /// Gets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; }

        /// <summary>
        /// Initializes a new instance of the SetStateBlock class.
        /// </summary>
        /// <param name="key">The key to set in the context state.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
        /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
        /// <param name="logger">Optional logger.</param>
        public SetStateBlock(
            string key,
            object value,
            string nextBlockOnSuccess = "",
            string nextBlockOnFailure = "",
            ILogger? logger = null) : base(logger)
        {
            _key = key;
            _value = value;
            NextBlockOnSuccess = nextBlockOnSuccess;
            NextBlockOnFailure = nextBlockOnFailure;
        }

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            LogInfo($"Setting state key '{_key}' to value '{_value}'");

            context.SetState(_key, _value);

            await Task.CompletedTask;
            return ExecutionResult.Success(NextBlockOnSuccess);
        }
    }

    /// <summary>
    /// A workflow block that conditionally executes based on a predicate.
    /// </summary>
    public class ConditionalBlock : WorkflowBlockBase
    {
        private readonly Func<ExecutionContext, bool> _condition;
        private readonly string _nextBlockOnConditionMet;
        private readonly string _nextBlockOnConditionNotMet;

        /// <summary>
        /// Gets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess => _nextBlockOnConditionMet;

        /// <summary>
        /// Gets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure => _nextBlockOnConditionNotMet;

        /// <summary>
        /// Initializes a new instance of the ConditionalBlock class.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="nextBlockOnConditionMet">The next block if condition is true.</param>
        /// <param name="nextBlockOnConditionNotMet">The next block if condition is false.</param>
        /// <param name="logger">Optional logger.</param>
        public ConditionalBlock(
            Func<ExecutionContext, bool> condition,
            string nextBlockOnConditionMet = "",
            string nextBlockOnConditionNotMet = "",
            ILogger? logger = null) : base(logger)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _nextBlockOnConditionMet = nextBlockOnConditionMet;
            _nextBlockOnConditionNotMet = nextBlockOnConditionNotMet;
        }

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            var conditionResult = _condition(context);

            LogInfo($"Condition evaluated to: {conditionResult}");

            var nextBlock = conditionResult ? _nextBlockOnConditionMet : _nextBlockOnConditionNotMet;

            await Task.CompletedTask;
            return ExecutionResult.Success(nextBlock);
        }
    }

    /// <summary>
    /// A workflow block that always fails for testing error handling.
    /// </summary>
    public class FailBlock : WorkflowBlockBase
    {
        private readonly string _errorMessage;

        /// <summary>
        /// Gets the name of the next block to execute on successful completion.
        /// </summary>
        public override string NextBlockOnSuccess { get; }

        /// <summary>
        /// Gets the name of the next block to execute on failure.
        /// </summary>
        public override string NextBlockOnFailure { get; }

        /// <summary>
        /// Initializes a new instance of the FailBlock class.
        /// </summary>
        /// <param name="errorMessage">The error message to fail with.</param>
        /// <param name="nextBlockOnSuccess">The next block to execute on success.</param>
        /// <param name="nextBlockOnFailure">The next block to execute on failure.</param>
        /// <param name="logger">Optional logger.</param>
        public FailBlock(
            string errorMessage = "Intentional failure for testing",
            string nextBlockOnSuccess = "",
            string nextBlockOnFailure = "",
            ILogger? logger = null) : base(logger)
        {
            _errorMessage = errorMessage;
            NextBlockOnSuccess = nextBlockOnSuccess;
            NextBlockOnFailure = nextBlockOnFailure;
        }

        /// <summary>
        /// Executes the core logic of the workflow block.
        /// </summary>
        /// <param name="context">The execution context containing input data, state, and services.</param>
        /// <returns>An execution result indicating the outcome and next block to execute.</returns>
        protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
        {
            LogError(new Exception(_errorMessage), "Intentional failure");

            await Task.CompletedTask;
            return ExecutionResult.Failure(NextBlockOnFailure, null, new Exception(_errorMessage));
        }
    }
}