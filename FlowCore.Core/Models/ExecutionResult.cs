namespace FlowCore.Models;

public class ExecutionResult
{
    public bool IsSuccess { get; }
    public string NextBlockName { get; }
    public object? Output { get; }
    public ExecutionMetadata Metadata { get; }
    public ExecutionStatus Status { get; }
    private ExecutionResult(
        bool isSuccess,
        string nextBlockName,
        object? output,
        ExecutionMetadata metadata,
        ExecutionStatus status)
    {
        IsSuccess = isSuccess;
        NextBlockName = nextBlockName;
        Output = output;
        Metadata = metadata;
        Status = status;
    }
    public static ExecutionResult Success(string? nextBlockName = null, object? output = null) => new(
            true,
            nextBlockName ?? string.Empty,
            output,
            new ExecutionMetadata(ExecutionStatus.Success, DateTime.UtcNow),
            ExecutionStatus.Success);
    public static ExecutionResult Failure(string? nextBlockName = null, object? output = null, Exception? error = null)
    {
        var metadata = new ExecutionMetadata(ExecutionStatus.Failure, DateTime.UtcNow);
        if (error != null)
        {
            metadata.AddError(error);
        }
        return new ExecutionResult(
            false,
            nextBlockName ?? string.Empty,
            output,
            metadata,
            ExecutionStatus.Failure);
    }
    public static ExecutionResult Skip(string? nextBlockName = null, string? reason = null)
    {
        var metadata = new ExecutionMetadata(ExecutionStatus.Skip, DateTime.UtcNow);
        if (!string.IsNullOrEmpty(reason))
        {
            metadata.AddInfo($"Block skipped: {reason}");
        }
        return new ExecutionResult(
            true,
            nextBlockName ?? string.Empty,
            null,
            metadata,
            ExecutionStatus.Skip);
    }
    public static ExecutionResult Wait(TimeSpan duration, string? nextBlockName = null, string? reason = null)
    {
        var metadata = new ExecutionMetadata(ExecutionStatus.Wait, DateTime.UtcNow);
        metadata.AddInfo($"Waiting for {duration.TotalSeconds} seconds{(string.IsNullOrEmpty(reason) ? "" : $": {reason}")}");
        return new ExecutionResult(
            true,
            nextBlockName ?? string.Empty,
            duration,
            metadata,
            ExecutionStatus.Wait);
    }
}
public enum ExecutionStatus
{
    Success,
    Failure,
    Skip,
    Wait
}
public class ExecutionMetadata(ExecutionStatus status, DateTime startedAt)
{
    private readonly List<string> _infoMessages = [];
    private readonly List<Exception> _errors = [];
    private readonly List<LogLevel> _logLevels = [];
    public ExecutionStatus Status { get; } = status;
    public DateTime StartedAt { get; } = startedAt;
    public DateTime CompletedAt { get; private set; } = startedAt; // Will be updated when execution completes
    public TimeSpan Duration => CompletedAt - StartedAt;
    public IReadOnlyList<string> InfoMessages => _infoMessages.AsReadOnly();
    public IReadOnlyList<Exception> Errors => _errors.AsReadOnly();
    public IReadOnlyList<LogLevel> LogLevels => _logLevels.AsReadOnly();
    public void MarkCompleted() => CompletedAt = DateTime.UtcNow;
    public void AddInfo(string message, LogLevel logLevel = LogLevel.Information)
    {
        _infoMessages.Add(message);
        _logLevels.Add(logLevel);
    }
    public void AddError(Exception error)
    {
        _errors.Add(error);
        _infoMessages.Add($"Error: {error.Message}");
        _logLevels.Add(LogLevel.Error);
    }
}
