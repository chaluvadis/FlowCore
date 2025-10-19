namespace LinkedListWorkflowEngine.Core.Models;
/// <summary>
/// Defines a guard configuration for workflow validation.
/// </summary>
public class GuardDefinition
{
    /// <summary>
    /// Gets the unique identifier for this guard definition.
    /// </summary>
    public string GuardId { get; }
    /// <summary>
    /// Gets the type of guard to create.
    /// </summary>
    public string GuardType { get; }
    /// <summary>
    /// Gets the assembly containing the guard type.
    /// </summary>
    public string AssemblyName { get; }
    /// <summary>
    /// Gets the namespace containing the guard type.
    /// </summary>
    public string? Namespace { get; }
    /// <summary>
    /// Gets the configuration parameters for the guard.
    /// </summary>
    public IReadOnlyDictionary<string, object> Configuration { get; }
    /// <summary>
    /// Gets the severity level for this guard.
    /// </summary>
    public GuardSeverity Severity { get; }
    /// <summary>
    /// Gets the category of this guard.
    /// </summary>
    public string Category { get; }
    /// <summary>
    /// Gets the name of the block to transition to if this guard fails.
    /// </summary>
    public string? FailureBlockName { get; }
    /// <summary>
    /// Gets a value indicating whether this is a pre-execution guard.
    /// </summary>
    public bool IsPreExecutionGuard { get; }
    /// <summary>
    /// Gets a value indicating whether this is a post-execution guard.
    /// </summary>
    public bool IsPostExecutionGuard { get; }
    /// <summary>
    /// Gets the display name for this guard.
    /// </summary>
    public string DisplayName { get; }
    /// <summary>
    /// Gets the description of what this guard validates.
    /// </summary>
    public string Description { get; }
    /// <summary>
    /// Initializes a new instance of the GuardDefinition class.
    /// </summary>
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
    public GuardDefinition(
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
        GuardId = guardId ?? throw new ArgumentNullException(nameof(guardId));
        GuardType = guardType ?? throw new ArgumentNullException(nameof(guardType));
        AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        Configuration = new ReadOnlyDictionary<string, object>(configuration ?? new Dictionary<string, object>());
        Severity = severity;
        Category = category ?? "General";
        FailureBlockName = failureBlockName;
        IsPreExecutionGuard = isPreExecutionGuard;
        IsPostExecutionGuard = isPostExecutionGuard;
        Namespace = @namespace;
        DisplayName = displayName ?? guardType;
        Description = description ?? $"Guard of type {guardType}";
    }
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