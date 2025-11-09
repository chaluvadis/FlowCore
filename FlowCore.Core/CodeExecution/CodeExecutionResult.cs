namespace FlowCore.CodeExecution;

/// <summary>
/// Represents the result of executing configurable code.
/// Contains execution status, output data, and error information.
/// </summary>
public class CodeExecutionResult
{
    /// <summary>
    /// Gets a value indicating whether the code execution was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the output data returned by the executed code, if any.
    /// </summary>
    public object? Output { get; }

    /// <summary>
    /// Gets the error message if the execution failed, or null if successful.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception that occurred during execution, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the execution time taken to run the code.
    /// </summary>
    public TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Gets additional metadata about the execution.
    /// </summary>
    public IDictionary<string, object> Metadata { get; }

    protected CodeExecutionResult(
        bool success,
        object? output,
        string? errorMessage,
        Exception? exception,
        TimeSpan executionTime,
        IDictionary<string, object>? metadata)
    {
        Success = success;
        Output = output;
        ErrorMessage = errorMessage;
        Exception = exception;
        ExecutionTime = executionTime;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    /// <param name="output">The output data from the execution.</param>
    /// <param name="executionTime">The time taken for execution.</param>
    /// <param name="metadata">Additional metadata about the execution.</param>
    /// <returns>A successful execution result.</returns>
    public static CodeExecutionResult CreateSuccess(
        object? output = null,
        TimeSpan? executionTime = null,
        IDictionary<string, object>? metadata = null) => new CodeExecutionResult(
            true,
            output,
            null,
            null,
            executionTime ?? TimeSpan.Zero,
            metadata);

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="executionTime">The time taken before failure.</param>
    /// <param name="metadata">Additional metadata about the failure.</param>
    /// <returns>A failed execution result.</returns>
    public static CodeExecutionResult CreateFailure(
        string? errorMessage = null,
        Exception? exception = null,
        TimeSpan? executionTime = null,
        IDictionary<string, object>? metadata = null) => new(
            false,
            null,
            errorMessage,
            exception,
            executionTime ?? TimeSpan.Zero,
            metadata);
}
