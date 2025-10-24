namespace FlowCore.CodeExecution.Management;

/// <summary>
/// Basic implementation of code block manager.
/// Provides in-memory management of code blocks with persistence support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the BasicCodeBlockManager.
/// </remarks>
/// <param name="logger">Optional logger for management operations.</param>
public class BasicCodeBlockManager(ILogger? logger = null) : ICodeBlockManager
{
    private readonly ConcurrentDictionary<string, CodeBlockDetails> _codeBlocks = new();
    private readonly ConcurrentDictionary<string, List<ExecutionRecord>> _executionHistory = new();
    private readonly object _statsLock = new();

    /// <summary>
    /// Gets all registered code blocks.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>A list of code block information.</returns>
    public IEnumerable<CodeBlockInfo> GetCodeBlocks(CodeBlockFilter? filter = null)
    {
        try
        {
            logger?.LogDebug("Getting code blocks with filter criteria");

            var blocks = _codeBlocks.Values.Select(details => (CodeBlockInfo)details);

            if (filter != null)
            {
                blocks = ApplyFilter(blocks, filter);
            }

            var result = blocks.ToList();
            logger?.LogDebug("Retrieved {Count} code blocks", result.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get code blocks");
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block.</param>
    /// <returns>Detailed code block information.</returns>
    public CodeBlockDetails? GetCodeBlockDetails(string blockId)
    {
        if (string.IsNullOrEmpty(blockId))
            return null;

        try
        {
            logger?.LogDebug("Getting details for code block {BlockId}", blockId);

            if (_codeBlocks.TryGetValue(blockId, out var details))
            {
                // Update execution statistics
                UpdateExecutionStatistics(details);

                logger?.LogDebug("Retrieved details for code block {BlockId}", blockId);
                return details;
            }

            logger?.LogWarning("Code block {BlockId} not found", blockId);
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get details for code block {BlockId}", blockId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new code block.
    /// </summary>
    /// <param name="definition">The code block definition.</param>
    /// <returns>The created code block information.</returns>
    public CodeBlockInfo CreateCodeBlock(CodeBlockDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        try
        {
            logger?.LogDebug("Creating new code block {Name}", definition.Name);

            var (validationResult, details) = PrepareCodeBlockDetails(definition);

            _codeBlocks.AddOrUpdate(details.BlockId, details, (_, _) => details);
            _executionHistory.TryAdd(details.BlockId, new List<ExecutionRecord>());

            logger?.LogInformation("Created code block {BlockId} ({Name})", details.BlockId, definition.Name);

            return details;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create code block {Name}", definition.Name);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block to update.</param>
    /// <param name="definition">The updated code block definition.</param>
    /// <returns>True if the update was successful.</returns>
    public bool UpdateCodeBlock(string blockId, CodeBlockDefinition definition)
    {
        if (string.IsNullOrEmpty(blockId) || definition == null)
            return false;

        try
        {
            logger?.LogDebug("Updating code block {BlockId}", blockId);

            if (!_codeBlocks.TryGetValue(blockId, out var existingDetails))
            {
                logger?.LogWarning("Code block {BlockId} not found for update", blockId);
                return false;
            }

            var (validationResult, updatedDetails) = PrepareCodeBlockDetails(definition, blockId);
            if (!validationResult.IsValid)
            {
                logger?.LogWarning("Code block validation failed for update: {Errors}",
                    string.Join(", ", validationResult.Errors));
                return false;
            }

            // Update the details
            existingDetails.Name = updatedDetails.Name;
            existingDetails.Description = updatedDetails.Description;
            existingDetails.Language = updatedDetails.Language;
            existingDetails.Mode = updatedDetails.Mode;
            existingDetails.IsEnabled = updatedDetails.IsEnabled;
            existingDetails.LastModified = updatedDetails.LastModified;
            existingDetails.Tags = updatedDetails.Tags;
            existingDetails.ExecutionConfig = updatedDetails.ExecutionConfig;
            existingDetails.SecurityConfig = updatedDetails.SecurityConfig;
            existingDetails.SourceCode = updatedDetails.SourceCode;
            existingDetails.LastValidation = updatedDetails.LastValidation;

            logger?.LogInformation("Updated code block {BlockId} ({Name})", blockId, definition.Name);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to update code block {BlockId}", blockId);
            return false;
        }
    }

    /// <summary>
    /// Deletes a code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block to delete.</param>
    /// <returns>True if the deletion was successful.</returns>
    public bool DeleteCodeBlock(string blockId)
    {
        if (string.IsNullOrEmpty(blockId))
            return false;

        try
        {
            logger?.LogDebug("Deleting code block {BlockId}", blockId);

            var blockRemoved = _codeBlocks.TryRemove(blockId, out var details);
            var historyRemoved = _executionHistory.TryRemove(blockId, out _);

            if (blockRemoved)
            {
                logger?.LogInformation("Deleted code block {BlockId} ({Name})", blockId, details?.Name);
            }

            return blockRemoved;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete code block {BlockId}", blockId);
            return false;
        }
    }

    /// <summary>
    /// Enables or disables a code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block.</param>
    /// <param name="enabled">Whether to enable or disable the block.</param>
    /// <returns>True if the operation was successful.</returns>
    public bool SetCodeBlockEnabled(string blockId, bool enabled)
    {
        if (string.IsNullOrEmpty(blockId))
            return false;

        try
        {
            logger?.LogDebug("Setting code block {BlockId} enabled = {Enabled}", blockId, enabled);

            if (_codeBlocks.TryGetValue(blockId, out var details))
            {
                details.IsEnabled = enabled;
                details.LastModified = DateTime.UtcNow;

                logger?.LogInformation("Set code block {BlockId} enabled = {Enabled}", blockId, enabled);

                return true;
            }

            logger?.LogWarning("Code block {BlockId} not found", blockId);
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to set enabled state for code block {BlockId}", blockId);
            return false;
        }
    }

    /// <summary>
    /// Validates a code block definition.
    /// </summary>
    /// <param name="definition">The definition to validate.</param>
    /// <returns>Validation results.</returns>
    public ValidationResult ValidateCodeBlock(CodeBlockDefinition definition)
    {
        if (definition == null)
            return ValidationResult.Failure(new[] { "Definition cannot be null" });

        var errors = new List<string>();

        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(definition.Name))
                errors.Add("Name is required");

            if (definition.ExecutionConfig == null)
                errors.Add("Execution configuration is required");
            else
            {
                // Validate execution config
                if (string.IsNullOrWhiteSpace(definition.ExecutionConfig.Language))
                    errors.Add("Language is required");

                if (definition.ExecutionConfig.Mode == CodeExecutionMode.Inline &&
                    string.IsNullOrWhiteSpace(definition.ExecutionConfig.Code))
                    errors.Add("Code is required for inline mode");

                if (definition.ExecutionConfig.Mode == CodeExecutionMode.Assembly &&
                    string.IsNullOrWhiteSpace(definition.ExecutionConfig.AssemblyPath))
                    errors.Add("Assembly path is required for assembly mode");
            }

            // Security validation
            if (definition.SecurityConfig != null)
            {
                if (definition.SecurityConfig.AllowedNamespaces.Count == 0 &&
                    definition.SecurityConfig.BlockedNamespaces.Count == 0)
                    errors.Add("At least one namespace restriction should be configured");
            }

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during code block validation");
            errors.Add($"Validation error: {ex.Message}");
            return ValidationResult.Failure(errors);
        }
    }

    /// <summary>
    /// Gets execution statistics for code blocks.
    /// </summary>
    /// <param name="timeRange">The time range for statistics.</param>
    /// <param name="blockIds">Optional specific block IDs to include.</param>
    /// <returns>Execution statistics.</returns>
    public CodeBlockExecutionStats GetExecutionStats(
        TimeRange timeRange,
        IEnumerable<string>? blockIds = null)
    {
        try
        {
            logger?.LogDebug("Getting execution statistics for time range {Start} to {End}",
                timeRange.Start, timeRange.End);

            var stats = new CodeBlockExecutionStats
            {
                TimeRange = timeRange
            };

            var targetBlockIds = blockIds?.ToList() ?? _executionHistory.Keys.ToList();

            lock (_statsLock)
            {
                foreach (var blockId in targetBlockIds)
                {
                    if (_executionHistory.TryGetValue(blockId, out var executions))
                    {
                        var relevantExecutions = executions
                            .Where(e => e.StartTime >= timeRange.Start && e.StartTime <= timeRange.End)
                            .ToList();

                        if (relevantExecutions.Count > 0)
                        {
                            var blockStats = CalculateBlockStats(blockId, relevantExecutions);
                            stats.BlockStats.Add(blockStats);

                            // Update overall stats
                            stats.TotalExecutions += blockStats.ExecutionCount;
                            stats.SuccessfulExecutions += (long)(blockStats.ExecutionCount * blockStats.SuccessRate / 100);
                            stats.FailedExecutions += blockStats.ExecutionCount - (long)(blockStats.ExecutionCount * blockStats.SuccessRate / 100);

                            // Update timing stats
                            if (stats.FastestExecution == TimeSpan.Zero || blockStats.AverageExecutionTime < stats.FastestExecution)
                                stats.FastestExecution = blockStats.AverageExecutionTime;

                            if (blockStats.AverageExecutionTime > stats.SlowestExecution)
                                stats.SlowestExecution = blockStats.AverageExecutionTime;
                        }
                    }
                }

                // Calculate overall average
                if (stats.BlockStats.Count > 0)
                {
                    stats.AverageExecutionTime = TimeSpan.FromTicks(
                        (long)stats.BlockStats.Average(bs => bs.AverageExecutionTime.Ticks));
                }
            }

            logger?.LogDebug("Generated execution statistics: {Total} total executions, {Success}% success rate",
                stats.TotalExecutions, stats.SuccessRate);

            return stats;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get execution statistics");
            throw;
        }
    }

    /// <summary>
    /// Records an execution for statistical purposes.
    /// </summary>
    /// <param name="blockId">The block ID.</param>
    /// <param name="execution">The execution record.</param>
    public void RecordExecution(string blockId, ExecutionRecord execution)
    {
        if (string.IsNullOrEmpty(blockId) || execution == null)
            return;

        try
        {
            if (_executionHistory.TryGetValue(blockId, out var executions))
            {
                lock (_statsLock)
                {
                    executions.Add(execution);

                    // Keep only the last 1000 executions to limit memory usage
                    if (executions.Count > 1000)
                    {
                        executions.RemoveRange(0, executions.Count - 1000);
                    }
                }

                // Update the block's execution summary
                if (_codeBlocks.TryGetValue(blockId, out var details))
                {
                    UpdateExecutionSummary(details, executions);
                }
            }

        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to record execution for block {BlockId}", blockId);
        }
    }

    private IEnumerable<CodeBlockInfo> ApplyFilter(IEnumerable<CodeBlockInfo> blocks, CodeBlockFilter filter)
    {
        var query = blocks.AsQueryable();

        query = ApplyNameFilter(query, filter.NamePattern);
        query = ApplyLanguageFilter(query, filter.Language);
        query = ApplyModeFilter(query, filter.Mode);
        query = ApplyEnabledFilter(query, filter.IsEnabled);
        query = ApplyTagsFilter(query, filter.RequiredTags);
        query = ApplyCreatedRangeFilter(query, filter.CreatedRange);
        query = ApplyMaxResultsFilter(query, filter.MaxResults);

        return query.ToList();
    }

    private IQueryable<CodeBlockInfo> ApplyNameFilter(IQueryable<CodeBlockInfo> query, string? namePattern)
    {
        if (!string.IsNullOrEmpty(namePattern))
        {
            var regex = new Regex(namePattern, RegexOptions.IgnoreCase);
            query = query.Where(b => regex.IsMatch(b.Name));
        }
        return query;
    }

    private IQueryable<CodeBlockInfo> ApplyLanguageFilter(IQueryable<CodeBlockInfo> query, string? language)
    {
        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(b => b.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
        }
        return query;
    }

    private IQueryable<CodeBlockInfo> ApplyModeFilter(IQueryable<CodeBlockInfo> query, CodeExecutionMode? mode)
    {
        if (mode.HasValue)
        {
            query = query.Where(b => b.Mode == mode.Value);
        }
        return query;
    }

    private IQueryable<CodeBlockInfo> ApplyEnabledFilter(IQueryable<CodeBlockInfo> query, bool? isEnabled)
    {
        if (isEnabled.HasValue)
        {
            query = query.Where(b => b.IsEnabled == isEnabled.Value);
        }
        return query;
    }

    private IQueryable<CodeBlockInfo> ApplyTagsFilter(IQueryable<CodeBlockInfo> query, List<string> requiredTags)
    {
        if (requiredTags.Count > 0)
        {
            query = query.Where(b => requiredTags.All(tag => b.Tags.Contains(tag)));
        }
        return query;
    }

    private IQueryable<CodeBlockInfo> ApplyCreatedRangeFilter(IQueryable<CodeBlockInfo> query, TimeRange? createdRange)
    {
        if (createdRange != null)
        {
            query = query.Where(b => b.CreatedAt >= createdRange.Start && b.CreatedAt <= createdRange.End);
        }
        return query;
    }

    private IQueryable<CodeBlockInfo> ApplyMaxResultsFilter(IQueryable<CodeBlockInfo> query, int? maxResults)
    {
        if (maxResults.HasValue)
        {
            query = query.Take(maxResults.Value);
        }
        return query;
    }

    private void UpdateExecutionStatistics(CodeBlockDetails details)
    {
        if (_executionHistory.TryGetValue(details.BlockId, out var executions))
        {
            UpdateExecutionSummary(details, executions);

            // Update recent executions (last 10)
            lock (_statsLock)
            {
                details.RecentExecutions = executions
                    .OrderByDescending(e => e.StartTime)
                    .Take(10)
                    .ToList();
            }
        }
    }

    /// <summary>
    /// Updates the execution summary for a code block.
    /// </summary>
    private void UpdateExecutionSummary(CodeBlockDetails details, List<ExecutionRecord> executions)
    {
        lock (_statsLock)
        {
            if (executions.Count > 0)
            {
                details.ExecutionSummary.TotalExecutions = executions.Count;
                details.ExecutionSummary.LastExecution = executions.Max(e => e.StartTime);
                details.ExecutionSummary.SuccessRate = executions.Count(e => e.Success) * 100.0 / executions.Count;
                details.ExecutionSummary.AverageExecutionTime = TimeSpan.FromTicks(
                    (long)executions.Average(e => e.Duration.Ticks));
            }
        }
    }

    private BlockExecutionStats CalculateBlockStats(string blockId, List<ExecutionRecord> executions)
    {
        var blockName = _codeBlocks.TryGetValue(blockId, out var details) ? details.Name : blockId;

        var successCount = executions.Count(e => e.Success);
        var successRate = executions.Count > 0 ? successCount * 100.0 / executions.Count : 0;
        var avgTime = executions.Count > 0 ?
            TimeSpan.FromTicks((long)executions.Average(e => e.Duration.Ticks)) :
            TimeSpan.Zero;

        // Analyze error patterns
        var errorPatterns = executions
            .Where(e => !e.Success && !string.IsNullOrEmpty(e.ErrorMessage))
            .GroupBy(e => e.ErrorMessage!)
            .Select(g => new ErrorPattern
            {
                Pattern = g.Key,
                Count = g.Count(),
                FirstOccurrence = g.Min(e => e.StartTime),
                LastOccurrence = g.Max(e => e.StartTime)
            })
            .OrderByDescending(ep => ep.Count)
            .Take(5)
            .ToList();

        return new BlockExecutionStats
        {
            BlockId = blockId,
            BlockName = blockName,
            ExecutionCount = executions.Count,
            SuccessRate = successRate,
            AverageExecutionTime = avgTime,
            LastExecution = executions.Count > 0 ? executions.Max(e => e.StartTime) : null,
            CommonErrors = errorPatterns
        };
    }

    private string GenerateBlockId(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "_");
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{sanitized}_{timestamp}";
    }

    private (ValidationResult validationResult, CodeBlockDetails details) PrepareCodeBlockDetails(CodeBlockDefinition definition, string? existingBlockId = null)
    {
        var validationResult = ValidateCodeBlock(definition);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Code block validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        var blockId = existingBlockId ?? GenerateBlockId(definition.Name);
        var now = DateTime.UtcNow;

        var details = new CodeBlockDetails
        {
            BlockId = blockId,
            Name = definition.Name,
            Description = definition.Description,
            Language = definition.ExecutionConfig.Language,
            Mode = definition.ExecutionConfig.Mode,
            IsEnabled = definition.IsEnabled,
            CreatedAt = existingBlockId != null ? DateTime.UtcNow : now, // For update, keep original created time
            LastModified = now,
            CreatedBy = "System",
            Tags = new List<string>(definition.Tags),
            ExecutionConfig = definition.ExecutionConfig,
            SecurityConfig = definition.SecurityConfig,
            SourceCode = definition.ExecutionConfig.Code,
            LastValidation = validationResult
        };

        return (validationResult, details);
    }
}