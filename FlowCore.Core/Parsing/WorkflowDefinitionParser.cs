namespace FlowCore.Parsing;
/// <summary>
/// Parses workflow definitions from JSON format into structured workflow objects.
/// Handles JSON deserialization, data transformation, and validation of workflow structure.
/// Supports parsing from both string content and file paths with comprehensive error handling.
/// </summary>
public class WorkflowDefinitionParser : IWorkflowParser
{
    /// <summary>
    /// Configured JSON serializer options for workflow definition parsing.
    /// Uses camelCase naming, case-insensitive properties, and supports comments and trailing commas.
    /// </summary>
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
    /// Parses a JSON string into a workflow definition object.
    /// This is the primary method for converting JSON workflow definitions into structured objects.
    /// </summary>
    /// <param name="json">The JSON string containing the workflow definition.</param>
    /// <returns>The parsed workflow definition object with all properties and blocks configured.</returns>
    /// <exception cref="ArgumentException">Thrown when json is null or empty.</exception>
    /// <exception cref="WorkflowParseException">Thrown when JSON parsing fails or the structure is invalid.</exception>
    public WorkflowDefinition ParseFromJson(string json)
    {
        // Validate input parameters
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new WorkflowParseException("JSON string cannot be null or empty.");
        }
        try
        {
            // Deserialize JSON into intermediate workflow definition object
            var jsonWorkflow = JsonSerializer.Deserialize<JsonWorkflowDefinition>(json, _jsonOptions);
            if (jsonWorkflow == null)
            {
                throw new WorkflowParseException("Failed to deserialize JSON workflow definition");
            }
            // Convert the deserialized object to the final workflow definition format
            return ConvertToWorkflowDefinition(jsonWorkflow);
        }
        catch (JsonException ex)
        {
            // Wrap JSON parsing errors in a workflow-specific exception
            throw new WorkflowParseException("Failed to parse workflow definition from JSON.", ex);
        }
    }
    /// <summary>
    /// Parses a workflow definition from a file containing JSON content.
    /// This method reads the file content and delegates to the string-based parser.
    /// </summary>
    /// <param name="path">The file system path to the JSON workflow definition file.</param>
    /// <returns>A task representing the parsed workflow definition object.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    /// <exception cref="WorkflowParseException">Thrown when file reading fails or JSON parsing fails.</exception>
    public async Task<WorkflowDefinition> ParseFromFileAsync(string path)
    {
        // Validate input parameters
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new WorkflowParseException("File path cannot be null or empty.");
        }
        try
        {
            // Read the entire file content as a string
            var json = await File.ReadAllTextAsync(path);
            // Delegate to the string-based parser for actual parsing logic
            return ParseFromJson(json);
        }
        catch (Exception ex) when (ex is not WorkflowParseException)
        {
            // Wrap file I/O errors in a workflow-specific exception
            throw new WorkflowParseException($"Failed to parse workflow definition from file '{path}'.", ex);
        }
    }
    /// <summary>
    /// Converts a deserialized JSON workflow definition into the structured workflow definition format.
    /// This method handles the transformation between the JSON representation and the internal domain model.
    /// </summary>
    /// <param name="jsonWorkflow">The deserialized JSON workflow definition object.</param>
    /// <returns>The converted workflow definition with all properties properly mapped.</returns>
    private WorkflowDefinition ConvertToWorkflowDefinition(JsonWorkflowDefinition jsonWorkflow)
    {
        // Convert workflow variables from JSON to dictionary format
        var variables = jsonWorkflow.Variables?.ToDictionary(
            v => v.Key,
            v => v.Value ?? string.Empty) ?? [];
        // Convert workflow blocks from JSON to structured block definitions
        var blocks = jsonWorkflow.Blocks?.ToDictionary(
            b => b.Name,
            b => ConvertToWorkflowBlockDefinition(b)) ?? [];
        // Convert global guards from JSON to guard definition objects
        var globalGuards = jsonWorkflow.GlobalGuards?.Select(ConvertToGuardDefinition).ToList()
            ?? [];
        // Convert block-specific guards from JSON to dictionary of guard lists
        var blockGuards = jsonWorkflow.BlockGuards?.ToDictionary(
            bg => bg.BlockName,
            bg => bg.Guards.Select(ConvertToGuardDefinition).ToList() as IList<GuardDefinition>)
            ?? [];
        // Convert workflow metadata using helper
        var metadata = ConvertToWorkflowMetadata(jsonWorkflow.Metadata);

        // Convert execution configuration using helper
        var executionConfig = ConvertToWorkflowExecutionConfig(jsonWorkflow.ExecutionConfig);
        // Create and return the final workflow definition using the factory method
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
    /// Converts a JSON block definition into a structured workflow block definition.
    /// This method handles the transformation of individual block configurations from JSON to domain objects.
    /// </summary>
    /// <param name="jsonBlock">The JSON block definition to convert.</param>
    /// <returns>The converted workflow block definition with all properties properly mapped.</returns>
    private WorkflowBlockDefinition ConvertToWorkflowBlockDefinition(JsonBlockDefinition jsonBlock) => new(
             jsonBlock.Id,
             jsonBlock.Type,
             jsonBlock.Assembly ?? "FlowCore", // Use default assembly if not specified
             jsonBlock.NextBlockOnSuccess ?? string.Empty, // Use empty string for optional transitions
             jsonBlock.NextBlockOnFailure ?? string.Empty,
             jsonBlock.Configuration ?? [], // Use empty config if not specified
             jsonBlock.Namespace,
             jsonBlock.Version,
             jsonBlock.DisplayName,
             jsonBlock.Description);

    /// <summary>
    /// Converts a JSON guard definition into a structured guard definition object.
    /// This method handles the transformation of guard configurations from JSON to domain objects.
    /// </summary>
    /// <param name="jsonGuard">The JSON guard definition to convert.</param>
    /// <returns>The converted guard definition with all properties properly mapped.</returns>
    private GuardDefinition ConvertToGuardDefinition(JsonGuardDefinition jsonGuard) => new(
             jsonGuard.Id,
             jsonGuard.Type,
             jsonGuard.Assembly ?? "FlowCore", // Use default assembly if not specified
             jsonGuard.Configuration ?? [], // Use empty config if not specified
             jsonGuard.Severity,
             "General", // Default category for guards
             null, // No parent guard by default
             true, // Enabled by default
             false, // Not a system guard by default
             jsonGuard.Namespace,
             jsonGuard.DisplayName,
             jsonGuard.Description);

    /// <summary>
    /// Converts JSON metadata into structured workflow metadata.
    /// </summary>
    private WorkflowMetadata ConvertToWorkflowMetadata(JsonWorkflowMetadata jsonMetadata)
    {
        var metadata = new WorkflowMetadata
        {
            Author = jsonMetadata.Author ?? string.Empty,
            CreatedAt = jsonMetadata.CreatedAt ?? DateTime.UtcNow,
            ModifiedAt = jsonMetadata.ModifiedAt ?? DateTime.UtcNow
        };

        if (jsonMetadata.Tags != null)
        {
            foreach (var tag in jsonMetadata.Tags)
            {
                metadata.Tags.Add(tag);
            }
        }

        if (jsonMetadata.CustomMetadata != null)
        {
            foreach (var kvp in jsonMetadata.CustomMetadata)
            {
                metadata.CustomMetadata[kvp.Key] = kvp.Value;
            }
        }

        return metadata;
    }

    /// <summary>
    /// Converts JSON execution configuration into structured workflow execution config.
    /// </summary>
    private WorkflowExecutionConfig ConvertToWorkflowExecutionConfig(JsonWorkflowExecutionConfig jsonExecutionConfig)
    {
        var executionConfig = new WorkflowExecutionConfig
        {
            Timeout = jsonExecutionConfig.Timeout ?? TimeSpan.FromMinutes(30),
            PersistStateAfterEachBlock = jsonExecutionConfig.PersistStateAfterEachBlock ?? true,
            MaxConcurrentBlocks = jsonExecutionConfig.MaxConcurrentBlocks ?? 1,
            EnableDetailedLogging = jsonExecutionConfig.EnableDetailedLogging ?? false
        };

        if (jsonExecutionConfig.RetryPolicy != null)
        {
            executionConfig.RetryPolicy = new RetryPolicy
            {
                MaxRetries = jsonExecutionConfig.RetryPolicy.MaxRetries,
                InitialDelay = jsonExecutionConfig.RetryPolicy.InitialDelay,
                MaxDelay = jsonExecutionConfig.RetryPolicy.MaxDelay,
                BackoffStrategy = jsonExecutionConfig.RetryPolicy.BackoffStrategy,
                BackoffMultiplier = jsonExecutionConfig.RetryPolicy.BackoffMultiplier
            };
        }

        return executionConfig;
    }
}