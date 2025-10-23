namespace FlowCore.CodeExecution;

/// <summary>
/// Interface for executing asynchronous configurable code within workflow blocks and guards.
/// Extends the basic code execution model with full async/await support.
/// </summary>
public interface IAsyncCodeExecutor : ICodeExecutor
{
    /// <summary>
    /// Executes asynchronous code with enhanced async context support.
    /// </summary>
    /// <param name="context">The execution context containing workflow state and configuration.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the asynchronous code execution result.</returns>
    Task<AsyncCodeExecutionResult> ExecuteAsyncCodeAsync(
        AsyncCodeExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether this executor can handle asynchronous execution patterns.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor supports async execution for the given config.</returns>
    bool SupportsAsyncExecution(CodeExecutionConfig config);

    /// <summary>
    /// Gets the maximum degree of parallelism supported by this executor.
    /// </summary>
    int MaxDegreeOfParallelism { get; }

    /// <summary>
    /// Gets a value indicating whether this executor supports concurrent execution.
    /// </summary>
    bool SupportsConcurrentExecution { get; }
}