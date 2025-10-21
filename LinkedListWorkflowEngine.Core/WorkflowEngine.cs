namespace LinkedListWorkflowEngine.Core;
using LinkedListWorkflowEngine.Core.Parsing;

/// <summary>
/// Main workflow engine that orchestrates the execution of workflow definitions.
/// Provides high-level workflow operations including execution, validation, parsing, and state management.
/// Acts as a facade that coordinates between the executor, validator, parser, and storage components.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowExecutor _executor;
    private readonly IWorkflowStore _workflowStore;
    private readonly IWorkflowParser _parser;
    private readonly IWorkflowValidator _validator;
    private readonly ILogger<WorkflowEngine>? _logger;

    /// <summary>
    /// Initializes a new instance of the WorkflowEngine with the specified dependencies.
    /// This is the recommended constructor for production use as it allows for proper dependency injection.
    /// </summary>
    /// <param name="executor">The workflow executor responsible for executing individual workflow blocks.</param>
    /// <param name="workflowStore">The workflow store for persisting execution state and checkpoints.</param>
    /// <param name="parser">The parser for converting workflow definitions from various formats.</param>
    /// <param name="validator">The validator for ensuring workflow definitions are valid before execution.</param>
    /// <param name="logger">Optional logger for recording workflow engine operations and diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
    public WorkflowEngine(
        IWorkflowExecutor executor,
        IWorkflowStore workflowStore,
        IWorkflowParser parser,
        IWorkflowValidator validator,
        ILogger<WorkflowEngine>? logger = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the WorkflowEngine using legacy configuration.
    /// This constructor is deprecated and should not be used for new implementations.
    /// </summary>
    /// <param name="blockFactory">The factory for creating workflow blocks.</param>
    /// <param name="stateManager">Optional state manager for workflow persistence.</param>
    /// <param name="logger">Optional logger for recording workflow engine operations.</param>
    /// <param name="stateManagerConfig">Optional configuration for the state manager.</param>
    [Obsolete("Use the new service-oriented constructor instead.")]
    public WorkflowEngine(
        IWorkflowBlockFactory blockFactory,
        IStateManager? stateManager = null,
        ILogger<WorkflowEngine>? logger = null,
        StateManagerConfig? stateManagerConfig = null)
        : this(
            executor: new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore()),
            workflowStore: new InMemoryWorkflowStore(),
            parser: new WorkflowDefinitionParser(),
            validator: new WorkflowValidator(),
            logger: logger)
    {
        _logger?.LogWarning("Using deprecated WorkflowEngine constructor. Consider migrating to service-oriented architecture.");
    }
    /// <summary>
    /// Executes a workflow asynchronously using the provided workflow definition and input data.
    /// This is the main entry point for workflow execution in the system.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition containing the structure and configuration of the workflow to execute.</param>
    /// <param name="input">The input data that will be available to the workflow during execution.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the workflow execution.</param>
    /// <returns>A task representing the workflow execution result containing success status, output data, and execution metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowDefinition or input is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the workflow definition fails validation.</exception>
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition workflowDefinition,
        object input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        ArgumentNullException.ThrowIfNull(input);

        // Validate the workflow definition before execution
        var validationResult = _validator.Validate(workflowDefinition);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Workflow definition '{workflowDefinition.Id}' is not valid: {string.Join(", ", validationResult.Errors)}");
        }

        _logger?.LogInformation("Starting execution of workflow {WorkflowId} v{Version}",
            workflowDefinition.Id, workflowDefinition.Version);

        // Create execution context with the provided input and workflow metadata
        var context = new ExecutionContext(input, cancellationToken, workflowDefinition.Name);

        // Delegate actual execution to the workflow executor
        return await _executor.ExecuteAsync(workflowDefinition, context, cancellationToken);
    }

    /// <summary>
    /// Resumes a workflow execution from a previously saved checkpoint.
    /// This allows workflows to continue execution after interruption or system restart.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to resume execution for.</param>
    /// <param name="executionId">The unique identifier of the execution to resume.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the resumed execution.</param>
    /// <returns>A task representing the resumed workflow execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowDefinition is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no checkpoint is found for the specified execution.</exception>
    public async Task<WorkflowExecutionResult> ResumeFromCheckpointAsync(
        WorkflowDefinition workflowDefinition,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);

        _logger?.LogInformation("Resuming workflow {WorkflowId} from checkpoint, execution {ExecutionId}",
            workflowDefinition.Id, executionId);

        // Delegate resume operation to the workflow executor
        return await _executor.ResumeAsync(workflowDefinition, executionId, cancellationToken);
    }
    /// <summary>
    /// Suspends a running workflow execution and saves its current state as a checkpoint.
    /// This allows the workflow to be resumed later from the point of suspension.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow to suspend.</param>
    /// <param name="executionId">The unique identifier of the execution to suspend.</param>
    /// <param name="context">The current execution context containing state and progress information.</param>
    /// <returns>A task representing the suspend operation.</returns>
    public async Task SuspendWorkflowAsync(string workflowId, Guid executionId, ExecutionContext context)
    {
        // Create a checkpoint capturing the current execution state
        var checkpoint = new ExecutionCheckpoint
        {
            WorkflowId = workflowId,
            ExecutionId = executionId,
            CurrentBlockName = context.CurrentBlockName,
            LastUpdatedUtc = DateTime.UtcNow,
            State = new Dictionary<string, object>(context.State),
            History = Array.Empty<BlockExecutionInfo>(),
            RetryCount = 0,
            CorrelationId = context.ExecutionId.ToString()
        };

        // Persist the checkpoint for later resumption
        await _workflowStore.SaveCheckpointAsync(checkpoint);
        _logger?.LogInformation("Workflow {WorkflowId}, execution {ExecutionId} suspended", workflowId, executionId);
    }

    /// <summary>
    /// Parses a JSON string into a workflow definition object.
    /// This is a convenience method that delegates to the configured parser.
    /// </summary>
    /// <param name="json">The JSON string containing the workflow definition.</param>
    /// <returns>The parsed workflow definition object.</returns>
    /// <exception cref="WorkflowParseException">Thrown when the JSON cannot be parsed into a valid workflow definition.</exception>
    public WorkflowDefinition ParseWorkflowDefinition(string json)
    {
        return _parser.ParseFromJson(json);
    }

    /// <summary>
    /// Validates a workflow definition to ensure it meets all structural and semantic requirements.
    /// This is a convenience method that delegates to the configured validator.
    /// </summary>
    /// <param name="workflowDefinition">The workflow definition to validate.</param>
    /// <returns>A validation result containing any errors or warnings found during validation.</returns>
    public ValidationResult ValidateWorkflowDefinition(WorkflowDefinition workflowDefinition)
    {
        return _validator.Validate(workflowDefinition);
    }
}