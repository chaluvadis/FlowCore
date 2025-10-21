namespace LinkedListWorkflowEngine.Core.Parsing;

/// <summary>
/// Handles parsing and validation of workflow definitions from various formats.
/// Provides user-friendly error messages and comprehensive validation.
/// </summary>
public class WorkflowDefinitionParser
{
    private readonly ILogger<WorkflowDefinitionParser>? _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public WorkflowDefinitionParser(ILogger<WorkflowDefinitionParser>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a JSON workflow definition with enhanced error reporting.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition string.</param>
    /// <returns>The parsed and validated workflow definition.</returns>
    /// <exception cref="WorkflowDefinitionException">Thrown when parsing or validation fails with detailed error information.</exception>
    public WorkflowDefinition ParseJsonDefinition(string jsonDefinition)
    {
        ArgumentNullException.ThrowIfNull(jsonDefinition);

        try
        {
            // First, validate JSON syntax
            var validationResult = ValidateJsonSyntax(jsonDefinition);
            if (!validationResult.IsValid)
            {
                throw new WorkflowDefinitionException(
                    $"JSON syntax error: {validationResult.ErrorMessage}",
                    validationResult.LineNumber,
                    validationResult.ColumnNumber);
            }

            // Parse the JSON
            var jsonWorkflow = JsonSerializer.Deserialize<JsonWorkflowDefinition>(jsonDefinition, _jsonOptions);
            if (jsonWorkflow == null)
            {
                throw new WorkflowDefinitionException("The JSON definition could not be parsed. Please check that all required fields are present.");
            }

            // Validate the parsed definition
            var validationErrors = ValidateWorkflowDefinition(jsonWorkflow);
            if (validationErrors.Any())
            {
                throw new WorkflowDefinitionException(
                    "Workflow definition validation failed:\n" + string.Join("\n", validationErrors.Select((error, index) => $"{index + 1}. {error}")),
                    "Please review your workflow definition and fix the issues above.");
            }

            return ConvertToWorkflowDefinition(jsonWorkflow);
        }
        catch (JsonException ex) when (ex.Message.Contains("at line"))
        {
            // Extract line and column information from JSON exceptions
            var (line, column) = ExtractLineAndColumn(ex.Message);
            throw new WorkflowDefinitionException(
                $"JSON parsing error: {ex.Message}",
                line,
                column,
                "Please check your JSON syntax and ensure all brackets and quotes are properly closed.");
        }
        catch (WorkflowDefinitionException)
        {
            throw; // Re-throw our custom exceptions as-is
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error while parsing workflow definition");
            throw new WorkflowDefinitionException(
                "An unexpected error occurred while parsing the workflow definition.",
                "Please check your workflow definition format and try again.");
        }
    }

    /// <summary>
    /// Validates JSON syntax and provides detailed error information.
    /// </summary>
    private static (bool IsValid, string ErrorMessage, int LineNumber, int ColumnNumber) ValidateJsonSyntax(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return (true, string.Empty, 0, 0);
        }
        catch (JsonException ex)
        {
            var (line, column) = ExtractLineAndColumn(ex.Message);
            return (false, ex.Message, line, column);
        }
    }

    /// <summary>
    /// Extracts line and column information from JSON exception messages.
    /// </summary>
    private static (int Line, int Column) ExtractLineAndColumn(string message)
    {
        var lineMatch = System.Text.RegularExpressions.Regex.Match(message, @"line (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var columnMatch = System.Text.RegularExpressions.Regex.Match(message, @"column (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var line = lineMatch.Success ? int.Parse(lineMatch.Groups[1].Value) : 0;
        var column = columnMatch.Success ? int.Parse(columnMatch.Groups[1].Value) : 0;

        return (line, column);
    }

    /// <summary>
    /// Validates the workflow definition for logical consistency and required fields.
    /// </summary>
    private static List<string> ValidateWorkflowDefinition(JsonWorkflowDefinition jsonWorkflow)
    {
        var errors = new List<string>();

        // Check required fields
        if (string.IsNullOrWhiteSpace(jsonWorkflow.Id))
            errors.Add("Workflow ID is required and cannot be empty.");

        if (string.IsNullOrWhiteSpace(jsonWorkflow.Name))
            errors.Add("Workflow name is required and cannot be empty.");

        if (string.IsNullOrWhiteSpace(jsonWorkflow.StartBlockName))
            errors.Add("Start block name is required. Specify which block should execute first.");

        // Validate blocks
        if (jsonWorkflow.Blocks == null || !jsonWorkflow.Blocks.Any())
        {
            errors.Add("At least one block must be defined in the workflow.");
        }
        else
        {
            // Check that start block exists
            if (!jsonWorkflow.Blocks.Any(b => b.Name == jsonWorkflow.StartBlockName))
                errors.Add($"Start block '{jsonWorkflow.StartBlockName}' was not found in the blocks collection.");

            // Validate each block
            foreach (var block in jsonWorkflow.Blocks)
            {
                var blockErrors = ValidateBlockDefinition(block);
                errors.AddRange(blockErrors.Select(error => $"Block '{block.Name}': {error}"));
            }

            // Check for circular references
            var circularRefs = FindCircularReferences(jsonWorkflow.Blocks);
            errors.AddRange(circularRefs.Select(block => $"Block '{block}': Creates a circular reference in the workflow."));
        }

        // Validate guards if present
        if (jsonWorkflow.GlobalGuards != null)
        {
            foreach (var guard in jsonWorkflow.GlobalGuards)
            {
                var guardErrors = ValidateGuardDefinition(guard);
                errors.AddRange(guardErrors.Select(error => $"Global guard '{guard.Id}': {error}"));
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a single block definition.
    /// </summary>
    private static List<string> ValidateBlockDefinition(JsonBlockDefinition block)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(block.Id))
            errors.Add("Block ID is required.");

        if (string.IsNullOrWhiteSpace(block.Name))
            errors.Add("Block name is required.");

        if (string.IsNullOrWhiteSpace(block.Type))
            errors.Add("Block type is required.");

        // Validate that referenced blocks exist (if specified)
        if (!string.IsNullOrWhiteSpace(block.NextBlockOnSuccess) && block.NextBlockOnSuccess != block.Name)
        {
            // This will be checked against all blocks later
        }

        return errors;
    }

    /// <summary>
    /// Validates a guard definition.
    /// </summary>
    private static List<string> ValidateGuardDefinition(JsonGuardDefinition guard)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(guard.Id))
            errors.Add("Guard ID is required.");

        if (string.IsNullOrWhiteSpace(guard.Type))
            errors.Add("Guard type is required.");

        return errors;
    }

    /// <summary>
    /// Detects circular references in block transitions.
    /// </summary>
    private static List<string> FindCircularReferences(List<JsonBlockDefinition> blocks)
    {
        var blockMap = blocks.ToDictionary(b => b.Name, b => b);
        var visited = new HashSet<string>();
        var circularRefs = new List<string>();

        foreach (var block in blocks)
        {
            if (!visited.Contains(block.Name))
            {
                var path = new List<string>();
                if (HasCircularReference(block.Name, blockMap, visited, path))
                {
                    circularRefs.AddRange(path);
                    break; // Stop at first circular reference found
                }
            }
        }

        return circularRefs.Distinct().ToList();
    }

    /// <summary>
    /// Recursively checks for circular references starting from a block.
    /// </summary>
    private static bool HasCircularReference(string currentBlock, Dictionary<string, JsonBlockDefinition> blockMap,
        HashSet<string> visited, List<string> path)
    {
        if (path.Contains(currentBlock))
        {
            path.Add(currentBlock);
            return true; // Found a cycle
        }

        if (!blockMap.TryGetValue(currentBlock, out var block) || visited.Contains(currentBlock))
            return false;

        path.Add(currentBlock);
        visited.Add(currentBlock);

        var nextBlocks = new[] { block.NextBlockOnSuccess, block.NextBlockOnFailure }
            .Where(name => !string.IsNullOrWhiteSpace(name) && name != currentBlock)
            .ToList();

        foreach (var nextBlock in nextBlocks)
        {
            if (HasCircularReference(nextBlock, blockMap, visited, path))
                return true;
        }

        path.RemoveAt(path.Count - 1);
        return false;
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
    private static WorkflowBlockDefinition ConvertToWorkflowBlockDefinition(JsonBlockDefinition jsonBlock)
    {
        return new WorkflowBlockDefinition(
            jsonBlock.Id,
            jsonBlock.Type,
            jsonBlock.Assembly ?? "LinkedListWorkflowEngine.Core",
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
    private static GuardDefinition ConvertToGuardDefinition(JsonGuardDefinition jsonGuard)
    {
        return new GuardDefinition(
            jsonGuard.Id,
            jsonGuard.Type,
            jsonGuard.Assembly ?? "LinkedListWorkflowEngine.Core",
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
}

/// <summary>
/// Exception thrown when workflow definition parsing or validation fails.
/// Provides detailed error information for better user experience.
/// </summary>
public class WorkflowDefinitionException : Exception
{
    public int LineNumber { get; }
    public int ColumnNumber { get; }
    public string UserFriendlyMessage { get; }

    public WorkflowDefinitionException(string message) : base(message)
    {
        UserFriendlyMessage = message;
    }

    public WorkflowDefinitionException(string message, int lineNumber, int columnNumber) : base(message)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        UserFriendlyMessage = message;
    }

    public WorkflowDefinitionException(string message, int lineNumber, int columnNumber, string userFriendlyMessage) : base(message)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        UserFriendlyMessage = userFriendlyMessage;
    }

    public WorkflowDefinitionException(string message, string userFriendlyMessage) : base(message)
    {
        UserFriendlyMessage = userFriendlyMessage;
    }
}