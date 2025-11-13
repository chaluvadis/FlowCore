namespace FlowCore.Models;
/// <summary>
/// Summary of guard evaluation results.
/// </summary>
public class GuardEvaluationSummary
{
    /// <summary>
    /// Gets the total number of guards evaluated.
    /// </summary>
    public int TotalGuards { get; internal set; }
    /// <summary>
    /// Gets the number of guards that passed.
    /// </summary>
    public int PassedGuards { get; internal set; }
    /// <summary>
    /// Gets the number of guards that failed.
    /// </summary>
    public int FailedGuards { get; internal set; }
    /// <summary>
    /// Gets the number of critical failures.
    /// </summary>
    public int CriticalFailures { get; internal set; }
    /// <summary>
    /// Gets the number of error failures.
    /// </summary>
    public int ErrorFailures { get; internal set; }
    /// <summary>
    /// Gets the number of warning failures.
    /// </summary>
    public int WarningFailures { get; internal set; }
    /// <summary>
    /// Gets the most severe failure result, if any.
    /// </summary>
    public GuardResult? MostSevereFailure { get; internal set; }
    /// <summary>
    /// Gets a value indicating whether all guards passed.
    /// </summary>
    public bool AllPassed => FailedGuards == 0;
    /// <summary>
    /// Gets a value indicating whether there are any critical failures.
    /// </summary>
    public bool HasCriticalFailures => CriticalFailures > 0;
    /// <summary>
    /// Gets a value indicating whether execution should be blocked.
    /// </summary>
    public bool ShouldBlockExecution => HasCriticalFailures || ErrorFailures > 0;
}
