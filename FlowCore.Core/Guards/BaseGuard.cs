namespace FlowCore.Guards;

/// <summary>
/// Base implementation for guard classes providing common functionality.
/// Reduces code duplication across guard implementations.
/// </summary>
public abstract class BaseGuard : IGuard
{
    /// <summary>
    /// Gets the unique identifier for this guard.
    /// </summary>
    public abstract string GuardId { get; }

    /// <summary>
    /// Gets the display name for this guard.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the description of what this guard validates.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the severity level of this guard.
    /// </summary>
    public virtual GuardSeverity Severity => GuardSeverity.Error;

    /// <summary>
    /// Gets the category of this guard for organizational purposes.
    /// </summary>
    public virtual string Category => "Validation";

    /// <summary>
    /// Evaluates the guard condition against the provided context.
    /// </summary>
    /// <param name="context">The execution context to evaluate against.</param>
    /// <returns>A guard result indicating whether the condition passed or failed.</returns>
    public abstract Task<GuardResult> EvaluateAsync(ExecutionContext context);

    /// <summary>
    /// Helper method to retrieve field value from execution context.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="fieldName">The name of the field to retrieve.</param>
    /// <returns>The field value or null if not found.</returns>
    protected static object? GetFieldValue(ExecutionContext context, string fieldName)
    {
        if (context.Input is IDictionary<string, object> inputDict && inputDict.TryGetValue(fieldName, out var inputValue))
        {
            return inputValue;
        }
        if (context.State.TryGetValue(fieldName, out var stateValue))
        {
            return stateValue;
        }
        return null;
    }

    /// <summary>
    /// Creates context data dictionary from key-value pairs.
    /// </summary>
    /// <param name="pairs">Key-value pairs to include in context data.</param>
    /// <returns>A dictionary containing the context data.</returns>
    protected static Dictionary<string, object> CreateContextData(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p => p.value);
}