namespace FlowCore.Parsing;

/// <summary>
/// JSON-based workflow engine that executes workflows defined declaratively in JSON.
/// Uses System.Text.Json with source generation for high-performance serialization.
/// </summary>
public class JsonWorkflowEngine : IJsonWorkflowEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowBlockFactory _blockFactory;
    private readonly IStateManager? _stateManager;
    private readonly WorkflowStatePersistenceService? _persistenceService;
    private readonly ILogger<JsonWorkflowEngine>? _logger;
    // JSON serialization options with source generation for performance
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates a WorkflowEngine instance for executing workflows.
    /// </summary>
    private WorkflowEngine CreateWorkflowEngine()
    {
        var workflowLogger = _logger as ILogger<WorkflowEngine> ?? new LoggerFactory().CreateLogger<WorkflowEngine>();
        return new WorkflowEngine(_blockFactory, _stateManager, workflowLogger);
    }
    public JsonWorkflowEngine(
        IServiceProvider serviceProvider,
        ILogger<JsonWorkflowEngine>? logger = null,
        IWorkflowBlockFactory? blockFactory = null,
        IStateManager? stateManager = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
        _blockFactory = blockFactory ?? new WorkflowBlockFactory(serviceProvider);
        _stateManager = stateManager;
        _persistenceService = _stateManager != null ? new WorkflowStatePersistenceService(_stateManager) : null;
    }
    /// <summary>
    /// Executes a workflow from a JSON definition string.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition.</param>
    /// <param name="input">The input data for the workflow.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The workflow execution result.</returns>
    public async Task<WorkflowExecutionResult> ExecuteFromJsonAsync(
        string jsonDefinition,
        object input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonDefinition);
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            // Parse and validate the JSON workflow definition
            var workflowDefinition = ParseWorkflowDefinition(jsonDefinition);
            // Use the existing WorkflowEngine to execute the parsed definition
            var engine = CreateWorkflowEngine();
            return await engine.ExecuteAsync(workflowDefinition, input, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse JSON workflow definition");
            throw new InvalidOperationException("Invalid JSON workflow definition", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute JSON workflow");
            throw;
        }
    }
    /// <summary>
    /// Executes a workflow from a JSON file.
    /// </summary>
    /// <param name="jsonFilePath">Path to the JSON workflow definition file.</param>
    /// <param name="input">The input data for the workflow.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The workflow execution result.</returns>
    public async Task<WorkflowExecutionResult> ExecuteFromJsonFileAsync(
        string jsonFilePath,
        object input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonFilePath);
        var jsonDefinition = await File.ReadAllTextAsync(jsonFilePath, cancellationToken);
        return await ExecuteFromJsonAsync(jsonDefinition, input, cancellationToken);
    }
    /// <summary>
    /// Parses a JSON workflow definition string into a WorkflowDefinition object.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition string.</param>
    /// <returns>The parsed WorkflowDefinition.</returns>
    public WorkflowDefinition ParseWorkflowDefinition(string jsonDefinition)
    {
        ArgumentNullException.ThrowIfNull(jsonDefinition);
        try
        {
            var jsonWorkflow = JsonSerializer.Deserialize<JsonWorkflowDefinition>(jsonDefinition, _jsonOptions);
            if (jsonWorkflow == null)
            {
                throw new InvalidOperationException("Failed to deserialize JSON workflow definition");
            }
            return ConvertToWorkflowDefinition(jsonWorkflow);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON deserialization failed for workflow definition");
            throw new InvalidOperationException("Invalid JSON workflow definition format", ex);
        }
    }
    /// <summary>
    /// Validates a JSON workflow definition string without executing it.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition string.</param>
    /// <returns>True if the definition is valid, false otherwise.</returns>
    public bool ValidateJsonDefinition(string jsonDefinition)
    {
        ArgumentNullException.ThrowIfNull(jsonDefinition);
        try
        {
            var jsonWorkflow = JsonSerializer.Deserialize<JsonWorkflowDefinition>(jsonDefinition, _jsonOptions);
            if (jsonWorkflow == null)
            {
                return false;
            }
            var workflowDefinition = ConvertToWorkflowDefinition(jsonWorkflow);
            return workflowDefinition.IsValid();
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Converts a JSON workflow definition to a WorkflowDefinition object.
    /// </summary>
    private WorkflowDefinition ConvertToWorkflowDefinition(JsonWorkflowDefinition jsonWorkflow)
    {
        // Convert variables
        var variables = jsonWorkflow.Variables?.ToDictionary(
            v => v.Key,
            v => v.Value ?? string.Empty) ?? [];
        // Convert blocks
        var blocks = jsonWorkflow.Blocks?.ToDictionary(
            b => b.Name,
            b => ConvertToWorkflowBlockDefinition(b)) ?? [];
        // Convert global guards
        var globalGuards = jsonWorkflow.GlobalGuards?.Select(ConvertToGuardDefinition).ToList()
            ?? [];
        // Convert block-specific guards
        var blockGuards = jsonWorkflow.BlockGuards?.ToDictionary(
            bg => bg.BlockName,
            bg => bg.Guards.Select(ConvertToGuardDefinition).ToList() as IList<GuardDefinition>)
            ?? [];
        // Create metadata
        var metadata = new WorkflowMetadata
        {
            Author = jsonWorkflow.Metadata?.Author ?? string.Empty,
            CreatedAt = jsonWorkflow.Metadata?.CreatedAt ?? DateTime.UtcNow,
            ModifiedAt = jsonWorkflow.Metadata?.ModifiedAt ?? DateTime.UtcNow
        };
        if (jsonWorkflow.Metadata?.Tags != null)
        {
            foreach (var tag in jsonWorkflow.Metadata.Tags)
            {
                metadata.Tags.Add(tag);
            }
        }
        // Add custom metadata if provided
        if (jsonWorkflow.Metadata?.CustomMetadata != null)
        {
            foreach (var kvp in jsonWorkflow.Metadata.CustomMetadata)
            {
                metadata.CustomMetadata[kvp.Key] = kvp.Value;
            }
        }
        // Create execution config
        var executionConfig = new WorkflowExecutionConfig
        {
            Timeout = jsonWorkflow.ExecutionConfig?.Timeout ?? TimeSpan.FromMinutes(30),
            PersistStateAfterEachBlock = jsonWorkflow.ExecutionConfig?.PersistStateAfterEachBlock ?? true,
            MaxConcurrentBlocks = jsonWorkflow.ExecutionConfig?.MaxConcurrentBlocks ?? 1,
            EnableDetailedLogging = jsonWorkflow.ExecutionConfig?.EnableDetailedLogging ?? false
        };
        if (jsonWorkflow.ExecutionConfig?.RetryPolicy != null)
        {
            executionConfig.RetryPolicy = new RetryPolicy
            {
                MaxRetries = jsonWorkflow.ExecutionConfig.RetryPolicy.MaxRetries,
                InitialDelay = jsonWorkflow.ExecutionConfig.RetryPolicy.InitialDelay,
                MaxDelay = jsonWorkflow.ExecutionConfig.RetryPolicy.MaxDelay,
                BackoffStrategy = jsonWorkflow.ExecutionConfig.RetryPolicy.BackoffStrategy,
                BackoffMultiplier = jsonWorkflow.ExecutionConfig.RetryPolicy.BackoffMultiplier
            };
        }
        return WorkflowDefinition.Create(
            jsonWorkflow.Id,
            jsonWorkflow.Name,
            jsonWorkflow.StartBlockName,
            blocks,
            jsonWorkflow.Version,
            jsonWorkflow.Description,
            metadata,
            executionConfig,
            variables,
            globalGuards,
            blockGuards);
    }
    /// <summary>
    /// Converts a JSON block definition to a WorkflowBlockDefinition object.
    /// </summary>
    private WorkflowBlockDefinition ConvertToWorkflowBlockDefinition(JsonBlockDefinition jsonBlock)
    {
        return new WorkflowBlockDefinition(
            jsonBlock.Id,
            jsonBlock.Type,
            jsonBlock.Assembly ?? "FlowCore",
            jsonBlock.NextBlockOnSuccess ?? string.Empty,
            jsonBlock.NextBlockOnFailure ?? string.Empty,
            jsonBlock.Configuration ?? [],
            jsonBlock.Namespace,
            jsonBlock.Version,
            jsonBlock.DisplayName,
            jsonBlock.Description);
    }
    /// <summary>
    /// Converts a JSON guard definition to a GuardDefinition object.
    /// </summary>
    private GuardDefinition ConvertToGuardDefinition(JsonGuardDefinition jsonGuard)
    {
        return new GuardDefinition(
            jsonGuard.Id,
            jsonGuard.Type,
            jsonGuard.Assembly ?? "FlowCore",
            jsonGuard.Configuration ?? [],
            jsonGuard.Severity,
            "General", // category
            null, // failureBlockName
            true, // isPreExecutionGuard
            false, // isPostExecutionGuard
            jsonGuard.Namespace,
            jsonGuard.DisplayName,
            jsonGuard.Description);
    }
    /// <summary>
    /// Executes a workflow asynchronously using the provided workflow definition and input data.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to execute.</param>
    /// <param name="input">The input data for the workflow execution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the workflow execution result.</returns>
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition workflowDefinition,
        object input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        ArgumentNullException.ThrowIfNull(input);
        // Use the existing WorkflowEngine to execute the parsed definition
        var engine = CreateWorkflowEngine();
        return await engine.ExecuteAsync(workflowDefinition, input, cancellationToken);
    }
    /// <summary>
    /// Resumes a workflow execution from a previously saved checkpoint.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to resume.</param>
    /// <param name="executionId">The execution ID to resume.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the resumed workflow execution result.</returns>
    public async Task<WorkflowExecutionResult> ResumeFromCheckpointAsync(
        WorkflowDefinition workflowDefinition,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        // Use the existing WorkflowEngine to resume execution
        var engine = CreateWorkflowEngine();
        return await engine.ResumeFromCheckpointAsync(workflowDefinition, executionId, cancellationToken);
    }
    /// <summary>
    /// Suspends a running workflow execution and saves its current state.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A task representing the suspend operation.</returns>
    public async Task SuspendWorkflowAsync(string workflowId, Guid executionId, ExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        ArgumentNullException.ThrowIfNull(context);
        // Use the existing WorkflowEngine to suspend execution
        var engine = CreateWorkflowEngine();
        await engine.SuspendWorkflowAsync(workflowId, executionId, context);
    }
}
/// <summary>
/// JSON-serializable workflow definition model.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(JsonWorkflowDefinition))]
public partial class JsonWorkflowDefinitionSourceGenerationContext : JsonSerializerContext
{
}
/// <summary>
/// JSON representation of a workflow definition.
/// </summary>
public class JsonWorkflowDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StartBlockName { get; set; } = string.Empty;
    public List<JsonBlockDefinition>? Blocks { get; set; }
    public List<JsonGuardDefinition>? GlobalGuards { get; set; }
    public List<JsonBlockGuards>? BlockGuards { get; set; }
    public JsonWorkflowMetadata? Metadata { get; set; }
    public JsonWorkflowExecutionConfig? ExecutionConfig { get; set; }
    public Dictionary<string, object>? Variables { get; set; }
}
/// <summary>
/// JSON representation of a workflow block definition.
/// </summary>
public class JsonBlockDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Assembly { get; set; }
    public string? Namespace { get; set; }
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? NextBlockOnSuccess { get; set; }
    public string? NextBlockOnFailure { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}
/// <summary>
/// JSON representation of a guard definition.
/// </summary>
public class JsonGuardDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Assembly { get; set; }
    public string? Namespace { get; set; }
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public GuardSeverity Severity { get; set; } = GuardSeverity.Error;
    public Dictionary<string, object>? Configuration { get; set; }
}
/// <summary>
/// JSON representation of block-specific guards.
/// </summary>
public class JsonBlockGuards
{
    public string BlockName { get; set; } = string.Empty;
    public List<JsonGuardDefinition> Guards { get; set; } = [];
}
/// <summary>
/// JSON representation of workflow metadata.
/// </summary>
public class JsonWorkflowMetadata
{
    public string? Author { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Dictionary<string, object>? CustomMetadata { get; set; }
}
/// <summary>
/// JSON representation of workflow execution configuration.
/// </summary>
public class JsonWorkflowExecutionConfig
{
    public TimeSpan? Timeout { get; set; }
    public bool? PersistStateAfterEachBlock { get; set; }
    public int? MaxConcurrentBlocks { get; set; }
    public bool? EnableDetailedLogging { get; set; }
    public JsonRetryPolicy? RetryPolicy { get; set; }
}
/// <summary>
/// JSON representation of retry policy.
/// </summary>
public class JsonRetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
    public double BackoffMultiplier { get; set; } = 2.0;
}