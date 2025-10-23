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
    public async Task<IEnumerable<CodeBlockInfo>> GetCodeBlocksAsync(CodeBlockFilter? filter = null)
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

            await Task.CompletedTask; // Make method async
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
    public async Task<CodeBlockDetails?> GetCodeBlockDetailsAsync(string blockId)
    {
        if (string.IsNullOrEmpty(blockId))
            return null;

        try
        {
            logger?.LogDebug("Getting details for code block {BlockId}", blockId);

            if (_codeBlocks.TryGetValue(blockId, out var details))
            {
                // Update execution statistics
                await UpdateExecutionStatisticsAsync(details);

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
    public async Task<CodeBlockInfo> CreateCodeBlockAsync(CodeBlockDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        try
        {
            logger?.LogDebug("Creating new code block {Name}", definition.Name);

            // Validate the definition
            var validationResult = await ValidateCodeBlockAsync(definition);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Code block validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            var blockId = GenerateBlockId(definition.Name);
            var now = DateTime.UtcNow;

            var details = new CodeBlockDetails
            {
                BlockId = blockId,
                Name = definition.Name,
                Description = definition.Description,
                Language = definition.ExecutionConfig.Language,
                Mode = definition.ExecutionConfig.Mode,
                IsEnabled = definition.IsEnabled,
                CreatedAt = now,
                LastModified = now,
                CreatedBy = "System", // In a real implementation, this would come from the current user
                Tags = new List<string>(definition.Tags),
                ExecutionConfig = definition.ExecutionConfig,
                SecurityConfig = definition.SecurityConfig,
                SourceCode = definition.ExecutionConfig.Code,
                LastValidation = validationResult
            };

            _codeBlocks.AddOrUpdate(blockId, details, (_, _) => details);
            _executionHistory.TryAdd(blockId, new List<ExecutionRecord>());

            logger?.LogInfo("Created code block {BlockId} ({Name})", blockId, definition.Name);

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
    public async Task<bool> UpdateCodeBlockAsync(string blockId, CodeBlockDefinition definition)
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

            // Validate the updated definition
            var validationResult = await ValidateCodeBlockAsync(definition);
            if (!validationResult.IsValid)
            {
                logger?.LogWarning("Code block validation failed for update: {Errors}",
                    string.Join(", ", validationResult.Errors));
                return false;
            }

            // Update the details
            existingDetails.Name = definition.Name;
            existingDetails.Description = definition.Description;
            existingDetails.Language = definition.ExecutionConfig.Language;
            existingDetails.Mode = definition.ExecutionConfig.Mode;
            existingDetails.IsEnabled = definition.IsEnabled;
            existingDetails.LastModified = DateTime.UtcNow;
            existingDetails.Tags = new List<string>(definition.Tags);
            existingDetails.ExecutionConfig = definition.ExecutionConfig;
            existingDetails.SecurityConfig = definition.SecurityConfig;
            existingDetails.SourceCode = definition.ExecutionConfig.Code;
            existingDetails.LastValidation = validationResult;

            logger?.LogInfo("Updated code block {BlockId} ({Name})", blockId, definition.Name);
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
    public async Task<bool> DeleteCodeBlockAsync(string blockId)
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
                logger?.LogInfo("Deleted code block {BlockId} ({Name})", blockId, details?.Name);
            }

            await Task.CompletedTask; // Make method async
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
    public async Task<bool> SetCodeBlockEnabledAsync(string blockId, bool enabled)
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

                logger?.LogInfo("Set code block {BlockId} enabled = {Enabled}", blockId, enabled);

                await Task.CompletedTask; // Make method async
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
    public async Task<ValidationResult> ValidateCodeBlockAsync(CodeBlockDefinition definition)
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

            await Task.CompletedTask; // Make method async
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
    public async Task<CodeBlockExecutionStats> GetExecutionStatsAsync(
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

            await Task.CompletedTask; // Make method async
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
    public async Task RecordExecutionAsync(string blockId, ExecutionRecord execution)
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
                    await UpdateExecutionSummaryAsync(details, executions);
                }
            }

            await Task.CompletedTask; // Make method async
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to record execution for block {BlockId}", blockId);
        }
    }

    private IEnumerable<CodeBlockInfo> ApplyFilter(IEnumerable<CodeBlockInfo> blocks, CodeBlockFilter filter)
    {
        var query = blocks.AsQueryable();

        if (!string.IsNullOrEmpty(filter.NamePattern))
        {
            var regex = new Regex(filter.NamePattern, RegexOptions.IgnoreCase);
            query = query.Where(b => regex.IsMatch(b.Name));
        }

        if (!string.IsNullOrEmpty(filter.Language))
        {
            query = query.Where(b => b.Language.Equals(filter.Language, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Mode.HasValue)
        {
            query = query.Where(b => b.Mode == filter.Mode.Value);
        }

        if (filter.IsEnabled.HasValue)
        {
            query = query.Where(b => b.IsEnabled == filter.IsEnabled.Value);
        }

        if (filter.RequiredTags.Count > 0)
        {
            query = query.Where(b => filter.RequiredTags.All(tag => b.Tags.Contains(tag)));
        }

        if (filter.CreatedRange != null)
        {
            query = query.Where(b => b.CreatedAt >= filter.CreatedRange.Start &&
                                   b.CreatedAt <= filter.CreatedRange.End);
        }

        if (filter.MaxResults.HasValue)
        {
            query = query.Take(filter.MaxResults.Value);
        }

        return query.ToList();
    }

    private async Task UpdateExecutionStatisticsAsync(CodeBlockDetails details)
    {
        if (_executionHistory.TryGetValue(details.BlockId, out var executions))
        {
            await UpdateExecutionSummaryAsync(details, executions);

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

    private async Task UpdateExecutionSummaryAsync(CodeBlockDetails details, List<ExecutionRecord> executions)
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

        await Task.CompletedTask; // Make method async
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
}