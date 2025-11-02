namespace FlowCore.Workflow;

/// <summary>
/// Extension methods for simplified workflow logging.
/// Reduces repetitive logging code throughout the workflow engine.
/// </summary>
public static class WorkflowLoggingExtensions
{
    /// <summary>
    /// Logs block execution completion with timing information.
    /// </summary>
    public static void LogBlockExecution(this ILogger logger, string blockName, TimeSpan duration, bool success) => logger.LogInformation("Block {BlockName} executed in {Duration}ms - {Status}",
                            blockName, duration.TotalMilliseconds, success ? "Success" : "Failed");

    /// <summary>
    /// Logs workflow completion with summary information.
    /// </summary>
    public static void LogWorkflowCompletion(this ILogger logger, string workflowId, TimeSpan? duration, bool success)
    {
        var durationMs = duration?.TotalMilliseconds ?? 0;
        logger.LogInformation("Workflow {WorkflowId} completed in {Duration}ms - {Status}",
                            workflowId, durationMs, success ? "Success" : "Failed");
    }

    /// <summary>
    /// Logs guard evaluation results.
    /// </summary>
    public static void LogGuardEvaluation(this ILogger logger, string blockName, bool passed, string? failureReason = null)
    {
        if (passed)
        {
            logger.LogDebug("Guard evaluation passed for block {BlockName}", blockName);
        }
        else
        {
            logger.LogWarning("Guard evaluation failed for block {BlockName}: {Reason}", blockName, failureReason);
        }
    }

    /// <summary>
    /// Logs checkpoint operations.
    /// </summary>
    public static void LogCheckpoint(this ILogger logger, string workflowId, string blockName, bool saved) => logger.LogDebug("Checkpoint {Action} for workflow {WorkflowId} at block {BlockName}",
                       saved ? "saved" : "loaded", workflowId, blockName);

    /// <summary>
    /// Logs error handling decisions.
    /// </summary>
    public static void LogErrorHandling(this ILogger logger, string blockName, string strategy, string reason) => logger.LogWarning("Error handling for block {BlockName}: {Strategy} - {Reason}", blockName, strategy, reason);
}