namespace FlowCore.Guards;
/// <summary>
/// Manages the evaluation of guard conditions for workflow blocks.
/// Handles both pre-execution and post-execution guard validation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the GuardManager class.
/// </remarks>
/// <param name="serviceProvider">The service provider for resolving guard dependencies.</param>
/// <param name="logger">Optional logger for guard evaluation.</param>
public class GuardManager(
    IServiceProvider serviceProvider,
    ILogger<GuardManager>? logger = null)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ConcurrentDictionary<string, (IEnumerable<GuardResult> results, DateTime timestamp)> _guardCache = new();

    /// <summary>
    /// Evaluates a collection of guards and returns the results.
    /// </summary>
    private async Task<List<GuardResult>> EvaluateGuardsAsync(IEnumerable<IGuard> guards, ExecutionContext context, bool useCache = false)
    {
        var results = new List<GuardResult>();
        logger?.LogDebug("Evaluating {GuardCount} guards", guards.Count());
        foreach (var guard in guards)
        {
            try
            {
                logger?.LogDebug("Evaluating guard: {GuardId}", guard.GuardId);
                var result = await guard.EvaluateAsync(context);
                results.Add(result);
                if (!result.IsValid)
                {
                    logger?.LogWarning("Guard {GuardId} failed: {ErrorMessage}", guard.GuardId, result.ErrorMessage);
                    // For critical guards, we might want to stop processing
                    if (result.Severity == GuardSeverity.Critical)
                    {
                        logger?.LogError("Critical guard {GuardId} failed, stopping guard evaluation", guard.GuardId);
                        break;
                    }
                }
                else
                {
                    logger?.LogDebug("Guard {GuardId} passed", guard.GuardId);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error evaluating guard {GuardId}", guard.GuardId);
                // Add a failure result for the exception
                results.Add(GuardResult.Failure(
                    $"Exception during guard evaluation: {ex.Message}",
                    severity: GuardSeverity.Error));
            }
        }
        return results;
    }

    /// <summary>
    /// Evaluates all pre-execution guards for a workflow block.
    /// </summary>
    /// <param name="guards">The collection of guards to evaluate.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A collection of guard results.</returns>
    public async Task<IEnumerable<GuardResult>> EvaluatePreExecutionGuardsAsync(
        IEnumerable<IGuard> guards,
        ExecutionContext context)
    {
        var key = string.Join("|", guards.Select(g => g.GuardId)) + "|" + context.GetHashCode();
        if (_guardCache.TryGetValue(key, out var cached))
        {
            if (DateTime.UtcNow - cached.timestamp < TimeSpan.FromMinutes(1))
            {
                logger?.LogDebug("Using cached guard results for key: {Key}", key);
                return cached.results;
            }
        }

        var results = await EvaluateGuardsAsync(guards, context, useCache: true);

        // Cache the results
        _guardCache[key] = (results, DateTime.UtcNow);

        // Evict if over limit
        if (_guardCache.Count > 100)
        {
            var oldest = _guardCache.OrderBy(kv => kv.Value.timestamp).First();
            _guardCache.TryRemove(oldest.Key, out _);
        }

        return results;
    }
    /// <summary>
    /// Evaluates all post-execution guards for a workflow block.
    /// </summary>
    /// <param name="guards">The collection of guards to evaluate.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="executionResult">The result of the block execution.</param>
    /// <returns>A collection of guard results.</returns>
    public async Task<IEnumerable<GuardResult>> EvaluatePostExecutionGuardsAsync(
        IEnumerable<IGuard> guards,
        ExecutionContext context,
        ExecutionResult executionResult)
    {
        return await EvaluateGuardsAsync(guards, context, useCache: false);
    }
    /// <summary>
    /// Determines if execution should be blocked based on guard results.
    /// </summary>
    /// <param name="guardResults">The results of guard evaluations.</param>
    /// <param name="blockOnWarnings">Whether to block execution on warning-level failures.</param>
    /// <returns>True if execution should be blocked, false otherwise.</returns>
   public static bool ShouldBlockExecution(IEnumerable<GuardResult> guardResults, bool blockOnWarnings = false)
    {
        if (guardResults == null) return false; // optional null-safety
        return guardResults.Any(r =>
            !r.IsValid &&
            (r.Severity == GuardSeverity.Critical ||
            r.Severity == GuardSeverity.Error ||
            (blockOnWarnings && r.Severity == GuardSeverity.Warning)));
    }
    /// <summary>
    /// Gets the most severe failure result from a collection of guard results.
    /// </summary>
    /// <param name="guardResults">The guard results to analyze.</param>
    /// <returns>The most severe failure result, or null if all guards passed.</returns>
    public GuardResult? GetMostSevereFailure(IEnumerable<GuardResult> guardResults)
    {
        var failedResults = guardResults.Where(r => !r.IsValid).ToList();
        if (failedResults.Count == 0)
        {
            return null;
        }
        // Order by severity (Critical > Error > Warning > Info)
        return failedResults.OrderByDescending(r => r.Severity).First();
    }
    /// <summary>
    /// Creates a comprehensive summary of guard evaluation results.
    /// </summary>
    /// <param name="guardResults">The guard results to summarize.</param>
    /// <returns>A summary of the guard evaluation results.</returns>
    public GuardEvaluationSummary CreateSummary(IEnumerable<GuardResult> guardResults)
    {
        var results = guardResults.ToList();
        return new GuardEvaluationSummary
        {
            TotalGuards = results.Count,
            PassedGuards = results.Count(r => r.IsValid),
            FailedGuards = results.Count(r => !r.IsValid),
            CriticalFailures = results.Count(r => r.Severity == GuardSeverity.Critical && !r.IsValid),
            ErrorFailures = results.Count(r => r.Severity == GuardSeverity.Error && !r.IsValid),
            WarningFailures = results.Count(r => r.Severity == GuardSeverity.Warning && !r.IsValid),
            MostSevereFailure = GetMostSevereFailure(results)
        };
    }
}