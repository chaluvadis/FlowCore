namespace FlowCore.CodeExecution;

/// <summary>
/// Interface for executing configurable code within workflow blocks and guards.
/// Provides a pluggable architecture for different code execution strategies.
/// </summary>
public interface ICodeExecutor
{
    /// <summary>
    /// Executes the configured code with the provided execution context.
    /// </summary>
    /// <param name="context">The execution context containing workflow state and configuration.</param>
    /// <param name="ct">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the code execution result with success status, output data, and any errors.</returns>
    Task<CodeExecutionResult> ExecuteAsync(
        CodeExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Determines whether this executor can handle the specified configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor can handle the configuration, false otherwise.</returns>
    bool CanExecute(CodeExecutionConfig config);

    /// <summary>
    /// Gets the list of programming languages supported by this executor.
    /// </summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    string ExecutorType { get; }

    /// <summary>
    /// Validates that the code can be executed safely with the given configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    ValidationResult ValidateExecutionSafety(CodeExecutionConfig config);
}
