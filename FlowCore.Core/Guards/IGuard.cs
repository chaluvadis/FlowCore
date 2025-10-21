namespace FlowCore.Guards;

/// <summary>
/// Interface for implementing guard conditions that validate workflow execution.
/// Guards can be used for pre-execution validation, post-execution validation,
/// and business rule enforcement.
/// </summary>
public interface IGuard
{
    /// <summary>
    /// Gets the unique identifier for this guard.
    /// </summary>
    string GuardId { get; }

    /// <summary>
    /// Gets the display name for this guard.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the description of what this guard validates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Evaluates the guard condition against the provided context.
    /// </summary>
    /// <param name="context">The execution context to evaluate against.</param>
    /// <returns>A guard result indicating whether the condition passed or failed.</returns>
    Task<GuardResult> EvaluateAsync(ExecutionContext context);

    /// <summary>
    /// Gets the severity level of this guard.
    /// </summary>
    GuardSeverity Severity { get; }

    /// <summary>
    /// Gets the category of this guard for organizational purposes.
    /// </summary>
    string Category { get; }
}

/// <summary>
/// Result of a guard evaluation.
/// </summary>
public class GuardResult
{
    /// <summary>
    /// Gets a value indicating whether the guard condition passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error message if the guard failed, or null if it passed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets additional context or details about the guard evaluation.
    /// </summary>
    public IDictionary<string, object> Context { get; }

    /// <summary>
    /// Gets the name of the block to transition to if this guard fails.
    /// </summary>
    public string? FailureBlockName { get; }

    /// <summary>
    /// Gets the severity of the guard failure.
    /// </summary>
    public GuardSeverity Severity { get; }

    private GuardResult(
        bool isValid,
        string? errorMessage,
        IDictionary<string, object>? context,
        string? failureBlockName,
        GuardSeverity severity)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        Context = context ?? new Dictionary<string, object>();
        FailureBlockName = failureBlockName;
        Severity = severity;
    }

    /// <summary>
    /// Creates a successful guard result.
    /// </summary>
    /// <param name="context">Optional context information.</param>
    /// <returns>A successful guard result.</returns>
    public static GuardResult Success(IDictionary<string, object>? context = null)
    {
        return new GuardResult(true, null, context, null, GuardSeverity.Info);
    }

    /// <summary>
    /// Creates a failed guard result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="failureBlockName">Optional block to transition to on failure.</param>
    /// <param name="severity">The severity of the failure.</param>
    /// <param name="context">Optional context information.</param>
    /// <returns>A failed guard result.</returns>
    public static GuardResult Failure(
        string errorMessage,
        string? failureBlockName = null,
        GuardSeverity severity = GuardSeverity.Error,
        IDictionary<string, object>? context = null)
    {
        return new GuardResult(false, errorMessage, context, failureBlockName, severity);
    }

    /// <summary>
    /// Creates a warning guard result.
    /// </summary>
    /// <param name="warningMessage">The warning message.</param>
    /// <param name="context">Optional context information.</param>
    /// <returns>A warning guard result.</returns>
    public static GuardResult Warning(string warningMessage, IDictionary<string, object>? context = null)
    {
        return new GuardResult(false, warningMessage, context, null, GuardSeverity.Warning);
    }
}

/// <summary>
/// Severity levels for guard conditions.
/// </summary>
public enum GuardSeverity
{
    /// <summary>
    /// Informational guard that doesn't affect execution flow.
    /// </summary>
    Info,

    /// <summary>
    /// Warning guard that logs issues but allows execution to continue.
    /// </summary>
    Warning,

    /// <summary>
    /// Error guard that blocks execution and requires attention.
    /// </summary>
    Error,

    /// <summary>
    /// Critical guard that represents a severe condition requiring immediate action.
    /// </summary>
    Critical
}