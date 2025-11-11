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

    // Default values for workflow components
    private const string DefaultAssembly = "FlowCore";
    private const string DefaultGuardCategory = "General";
    private const string EmptyString = "";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
    private const bool DefaultPersistState = true;
    private const int DefaultMaxConcurrentBlocks = 1;
    private const bool DefaultEnableDetailedLogging = false;
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
            var jsonWorkflow = JsonSerializer.Deserialize<JsonWorkflowDefinition>(json, _jsonOptions)
                    ?? throw new WorkflowParseException("Failed to deserialize JSON workflow definition");

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
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
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
    private static WorkflowDefinition ConvertToWorkflowDefinition(JsonWorkflowDefinition jsonWorkflow)
    {
        var variables = ConvertVariables(jsonWorkflow);
        var blocks = ConvertBlocks(jsonWorkflow);
        var globalGuards = ConvertGlobalGuards(jsonWorkflow);
        var blockGuards = ConvertBlockGuards(jsonWorkflow);
        var metadata = ConvertToWorkflowMetadata(jsonWorkflow.Metadata);
        var executionConfig = ConvertToWorkflowExecutionConfig(jsonWorkflow.ExecutionConfig);

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

    private static Dictionary<string, object> ConvertVariables(JsonWorkflowDefinition jsonWorkflow)
        => jsonWorkflow.Variables?.ToDictionary(v => v.Key, v => v.Value ?? EmptyString) ?? [];

    private static Dictionary<string, WorkflowBlockDefinition> ConvertBlocks(JsonWorkflowDefinition jsonWorkflow)
        => jsonWorkflow.Blocks?.ToDictionary(block => block.Id, ConvertToWorkflowBlockDefinition) ?? [];

    private static List<GuardDefinition> ConvertGlobalGuards(JsonWorkflowDefinition jsonWorkflow)
        => jsonWorkflow.GlobalGuards?.Select(ConvertToGuardDefinition).ToList() ?? [];

    private static Dictionary<string, IList<GuardDefinition>> ConvertBlockGuards(JsonWorkflowDefinition jsonWorkflow)
        => jsonWorkflow.BlockGuards?.ToDictionary(
            bg => bg.BlockName,
            bg => bg.Guards.Select(g => ConvertToGuardDefinition(g)).ToList() as IList<GuardDefinition>) ?? [];
    /// <summary>
    /// Converts a JSON block definition into a structured workflow block definition.
    /// This method handles the transformation of individual block configurations from JSON to domain objects.
    /// </summary>
    /// <param name="jsonBlock">The JSON block definition to convert.</param>
    /// <returns>The converted workflow block definition with all properties properly mapped.</returns>
    private static WorkflowBlockDefinition ConvertToWorkflowBlockDefinition(JsonBlockDefinition jsonBlock)
        => new(
              jsonBlock.Id,
              jsonBlock.Type,
              jsonBlock.Assembly ?? DefaultAssembly, // Use default assembly if not specified
              jsonBlock.NextBlockOnSuccess ?? EmptyString, // Use empty string for optional transitions
              jsonBlock.NextBlockOnFailure ?? EmptyString,
              jsonBlock.Configuration ?? [], // Use empty config if not specified
              jsonBlock.Namespace,
              jsonBlock.Version,
              jsonBlock.DisplayName,
              jsonBlock.Description
         );

    /// <summary>
    /// Converts a JSON guard definition into a structured guard definition object.
    /// This method handles the transformation of guard configurations from JSON to domain objects.
    /// </summary>
    /// <param name="jsonGuard">The JSON guard definition to convert.</param>
    /// <returns>The converted guard definition with all properties properly mapped.</returns>
    private static GuardDefinition ConvertToGuardDefinition(JsonGuardDefinition jsonGuard)
        => new(
              jsonGuard.Id,
              jsonGuard.Type,
              jsonGuard.Assembly ?? DefaultAssembly, // Use default assembly if not specified
              jsonGuard.Configuration ?? [], // Use empty config if not specified
              jsonGuard.Severity,
              DefaultGuardCategory, // Default category for guards
              null, // No parent guard by default
              true, // Enabled by default
              false, // Not a system guard by default
              jsonGuard.Namespace,
              jsonGuard.DisplayName,
              jsonGuard.Description
         );

    /// <summary>
    /// Converts JSON metadata into structured workflow metadata.
    /// </summary>
    private static WorkflowMetadata ConvertToWorkflowMetadata(JsonWorkflowMetadata? jsonMetadata)
    {
        var metadata = new WorkflowMetadata
        {
            Author = jsonMetadata?.Author ?? EmptyString,
            CreatedAt = jsonMetadata?.CreatedAt ?? DateTime.UtcNow,
            ModifiedAt = jsonMetadata?.ModifiedAt ?? DateTime.UtcNow
        };

        if (jsonMetadata?.Tags != null)
        {
            foreach (var tag in jsonMetadata.Tags)
            {
                metadata.Tags.Add(tag);
            }
        }

        if (jsonMetadata?.CustomMetadata != null)
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
    private static WorkflowExecutionConfig ConvertToWorkflowExecutionConfig(JsonWorkflowExecutionConfig? jsonExecutionConfig)
    {
        if (jsonExecutionConfig == null)
        {
            return new WorkflowExecutionConfig
            {
                Timeout = DefaultTimeout,
                PersistStateAfterEachBlock = DefaultPersistState,
                MaxConcurrentBlocks = DefaultMaxConcurrentBlocks,
                EnableDetailedLogging = DefaultEnableDetailedLogging
            };
        }

        var executionConfig = new WorkflowExecutionConfig
        {
            Timeout = jsonExecutionConfig.Timeout ?? DefaultTimeout,
            PersistStateAfterEachBlock = jsonExecutionConfig.PersistStateAfterEachBlock ?? DefaultPersistState,
            MaxConcurrentBlocks = jsonExecutionConfig.MaxConcurrentBlocks ?? DefaultMaxConcurrentBlocks,
            EnableDetailedLogging = jsonExecutionConfig.EnableDetailedLogging ?? DefaultEnableDetailedLogging
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
