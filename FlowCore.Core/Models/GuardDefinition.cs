namespace FlowCore.Models;
/// <summary>
/// Defines a guard configuration for workflow validation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the GuardDefinition class.
/// </remarks>
/// <param name="guardId">The unique identifier for this guard.</param>
/// <param name="guardType">The type of guard to create.</param>
/// <param name="assemblyName">The assembly containing the guard type.</param>
/// <param name="configuration">The configuration parameters for the guard.</param>
/// <param name="severity">The severity level for this guard.</param>
/// <param name="category">The category of this guard.</param>
/// <param name="failureBlockName">The name of the block to transition to if this guard fails.</param>
/// <param name="isPreExecutionGuard">Whether this is a pre-execution guard.</param>
/// <param name="isPostExecutionGuard">Whether this is a post-execution guard.</param>
/// <param name="namespace">The namespace containing the guard type.</param>
/// <param name="displayName">The display name for this guard.</param>
/// <param name="description">The description of what this guard validates.</param>
public class GuardDefinition(
    string guardId,
    string guardType,
    string assemblyName,
    IDictionary<string, object>? configuration = null,
    GuardSeverity severity = GuardSeverity.Error,
    string category = "General",
    string? failureBlockName = null,
    bool isPreExecutionGuard = true,
    bool isPostExecutionGuard = false,
    string? @namespace = null,
    string? displayName = null,
    string? description = null)
{
    /// <summary>
    /// Gets the unique identifier for this guard definition.
    /// </summary>
    public string GuardId { get; } = guardId ?? throw new ArgumentNullException(nameof(guardId));
    /// <summary>
    /// Gets the type of guard to create.
    /// </summary>
    public string GuardType { get; } = guardType ?? throw new ArgumentNullException(nameof(guardType));
    /// <summary>
    /// Gets the assembly containing the guard type.
    /// </summary>
    public string AssemblyName { get; } = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
    /// <summary>
    /// Gets the namespace containing the guard type.
    /// </summary>
    public string? Namespace { get; } = @namespace;
    /// <summary>
    /// Gets the configuration parameters for the guard.
    /// </summary>
    public IReadOnlyDictionary<string, object> Configuration { get; } = new ReadOnlyDictionary<string, object>(configuration ?? new Dictionary<string, object>());
    /// <summary>
    /// Gets the severity level for this guard.
    /// </summary>
    public GuardSeverity Severity { get; } = severity;
    /// <summary>
    /// Gets the category of this guard.
    /// </summary>
    public string Category { get; } = category ?? "General";
    /// <summary>
    /// Gets the name of the block to transition to if this guard fails.
    /// </summary>
    public string? FailureBlockName { get; } = failureBlockName;
    /// <summary>
    /// Gets a value indicating whether this is a pre-execution guard.
    /// </summary>
    public bool IsPreExecutionGuard { get; } = isPreExecutionGuard;
    /// <summary>
    /// Gets a value indicating whether this is a post-execution guard.
    /// </summary>
    public bool IsPostExecutionGuard { get; } = isPostExecutionGuard;
    /// <summary>
    /// Gets the display name for this guard.
    /// </summary>
    public string DisplayName { get; } = displayName ?? guardType;
    /// <summary>
    /// Gets the description of what this guard validates.
    /// </summary>
    public string Description { get; } = description ?? $"Guard of type {guardType}";

    /// <summary>
    /// Creates a pre-execution guard definition.
    /// </summary>
    /// <param name="guardId">The unique identifier for this guard.</param>
    /// <param name="guardType">The type of guard to create.</param>
    /// <param name="assemblyName">The assembly containing the guard type.</param>
    /// <param name="configuration">The configuration parameters for the guard.</param>
    /// <param name="severity">The severity level for this guard.</param>
    /// <param name="category">The category of this guard.</param>
    /// <param name="failureBlockName">The name of the block to transition to if this guard fails.</param>
    /// <param name="namespace">The namespace containing the guard type.</param>
    /// <param name="displayName">The display name for this guard.</param>
    /// <param name="description">The description of what this guard validates.</param>
    /// <returns>A pre-execution guard definition.</returns>
    public static GuardDefinition CreatePreExecution(
        string guardId,
        string guardType,
        string assemblyName,
        IDictionary<string, object>? configuration = null,
        GuardSeverity severity = GuardSeverity.Error,
        string category = "General",
        string? failureBlockName = null,
        string? @namespace = null,
        string? displayName = null,
        string? description = null)
    {
        return new GuardDefinition(
            guardId,
            guardType,
            assemblyName,
            configuration,
            severity,
            category,
            failureBlockName,
            isPreExecutionGuard: true,
            isPostExecutionGuard: false,
            @namespace,
            displayName,
            description);
    }
    /// <summary>
    /// Creates a post-execution guard definition.
    /// </summary>
    /// <param name="guardId">The unique identifier for this guard.</param>
    /// <param name="guardType">The type of guard to create.</param>
    /// <param name="assemblyName">The assembly containing the guard type.</param>
    /// <param name="configuration">The configuration parameters for the guard.</param>
    /// <param name="severity">The severity level for this guard.</param>
    /// <param name="category">The category of this guard.</param>
    /// <param name="failureBlockName">The name of the block to transition to if this guard fails.</param>
    /// <param name="namespace">The namespace containing the guard type.</param>
    /// <param name="displayName">The display name for this guard.</param>
    /// <param name="description">The description of what this guard validates.</param>
    /// <returns>A post-execution guard definition.</returns>
    public static GuardDefinition CreatePostExecution(
        string guardId,
        string guardType,
        string assemblyName,
        IDictionary<string, object>? configuration = null,
        GuardSeverity severity = GuardSeverity.Error,
        string category = "General",
        string? failureBlockName = null,
        string? @namespace = null,
        string? displayName = null,
        string? description = null)
    {
        return new GuardDefinition(
            guardId,
            guardType,
            assemblyName,
            configuration,
            severity,
            category,
            failureBlockName,
            isPreExecutionGuard: false,
            isPostExecutionGuard: true,
            @namespace,
            displayName,
            description);
    }
}