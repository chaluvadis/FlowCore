namespace FlowCore.CodeExecution.Debugging;

/// <summary>
/// Interface for debugging code execution.
/// Provides capabilities for setting breakpoints, inspecting variables, and stepping through code.
/// </summary>
public interface ICodeExecutionDebugger
{
    /// <summary>
    /// Gets a value indicating whether debugging is currently enabled.
    /// </summary>
    bool IsDebuggingEnabled { get; }

    /// <summary>
    /// Gets the current debugging session, if any.
    /// </summary>
    IDebugSession? CurrentSession { get; }

    /// <summary>
    /// Starts a new debugging session.
    /// </summary>
    /// <param name="config">Configuration for the debugging session.</param>
    /// <returns>The created debug session.</returns>
    Task<IDebugSession> StartDebugSessionAsync(DebugConfiguration config);

    /// <summary>
    /// Stops the current debugging session.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    Task StopDebugSessionAsync();

    /// <summary>
    /// Sets a breakpoint at the specified location.
    /// </summary>
    /// <param name="breakpoint">The breakpoint to set.</param>
    /// <returns>True if the breakpoint was set successfully.</returns>
    Task<bool> SetBreakpointAsync(Breakpoint breakpoint);

    /// <summary>
    /// Removes a breakpoint.
    /// </summary>
    /// <param name="breakpointId">The ID of the breakpoint to remove.</param>
    /// <returns>True if the breakpoint was removed successfully.</returns>
    Task<bool> RemoveBreakpointAsync(string breakpointId);

    /// <summary>
    /// Gets all active breakpoints.
    /// </summary>
    /// <returns>A list of active breakpoints.</returns>
    Task<IEnumerable<Breakpoint>> GetBreakpointsAsync();

    /// <summary>
    /// Executes code with debugging support.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The debug execution result.</returns>
    Task<DebugExecutionResult> ExecuteWithDebuggingAsync(
        CodeExecutionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Represents a debugging session for code execution.
/// </summary>
public interface IDebugSession : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this debug session.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Gets the current execution state of the debug session.
    /// </summary>
    DebugSessionState State { get; }

    /// <summary>
    /// Gets the current execution context being debugged.
    /// </summary>
    CodeExecutionContext? CurrentContext { get; }

    /// <summary>
    /// Gets the current call stack.
    /// </summary>
    IReadOnlyList<StackFrame> CallStack { get; }

    /// <summary>
    /// Gets the current variables in scope.
    /// </summary>
    IReadOnlyDictionary<string, object> Variables { get; }

    /// <summary>
    /// Event fired when execution hits a breakpoint.
    /// </summary>
    event EventHandler<BreakpointHitEventArgs> BreakpointHit;

    /// <summary>
    /// Event fired when the debug session state changes.
    /// </summary>
    event EventHandler<DebugSessionStateChangedEventArgs> StateChanged;

    /// <summary>
    /// Continues execution until the next breakpoint or completion.
    /// </summary>
    /// <returns>A task representing the continue operation.</returns>
    Task ContinueAsync();

    /// <summary>
    /// Steps to the next line of code.
    /// </summary>
    /// <returns>A task representing the step operation.</returns>
    Task StepOverAsync();

    /// <summary>
    /// Steps into the next function call.
    /// </summary>
    /// <returns>A task representing the step operation.</returns>
    Task StepIntoAsync();

    /// <summary>
    /// Steps out of the current function.
    /// </summary>
    /// <returns>A task representing the step operation.</returns>
    Task StepOutAsync();

    /// <summary>
    /// Evaluates an expression in the current context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The result of the expression evaluation.</returns>
    Task<object?> EvaluateExpressionAsync(string expression);

    /// <summary>
    /// Gets the value of a variable.
    /// </summary>
    /// <param name="variableName">The name of the variable.</param>
    /// <returns>The variable value.</returns>
    Task<object?> GetVariableValueAsync(string variableName);

    /// <summary>
    /// Sets the value of a variable.
    /// </summary>
    /// <param name="variableName">The name of the variable.</param>
    /// <param name="value">The new value.</param>
    /// <returns>True if the variable was set successfully.</returns>
    Task<bool> SetVariableValueAsync(string variableName, object? value);
}

/// <summary>
/// Configuration for debugging sessions.
/// </summary>
public class DebugConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to break on all exceptions.
    /// </summary>
    public bool BreakOnExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to break on first line.
    /// </summary>
    public bool BreakOnFirstLine { get; set; }

    /// <summary>
    /// Gets or sets the maximum call stack depth to track.
    /// </summary>
    public int MaxCallStackDepth { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of variables to track per scope.
    /// </summary>
    public int MaxVariablesPerScope { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the debugging timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets a value indicating whether to enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the types of events to log.
    /// </summary>
    public DebugEventTypes LogEventTypes { get; set; } = DebugEventTypes.All;

    /// <summary>
    /// Gets the default debug configuration.
    /// </summary>
    public static DebugConfiguration Default => new();
}

/// <summary>
/// Represents a breakpoint in code execution.
/// </summary>
public class Breakpoint
{
    /// <summary>
    /// Gets or sets the unique identifier for this breakpoint.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the line number where the breakpoint is set.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the column number where the breakpoint is set.
    /// </summary>
    public int? ColumnNumber { get; set; }

    /// <summary>
    /// Gets or sets the condition that must be true for the breakpoint to trigger.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets the hit count condition for the breakpoint.
    /// </summary>
    public HitCountCondition? HitCountCondition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the breakpoint is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of times this breakpoint has been hit.
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this breakpoint.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

}

/// <summary>
/// Represents a condition based on hit count for breakpoints.
/// </summary>
public class HitCountCondition
{
    /// <summary>
    /// Gets or sets the type of hit count condition.
    /// </summary>
    public HitCountConditionType Type { get; set; }

    /// <summary>
    /// Gets or sets the target hit count value.
    /// </summary>
    public int Value { get; set; }
}

/// <summary>
/// Types of hit count conditions.
/// </summary>
public enum HitCountConditionType
{
    /// <summary>
    /// Break when hit count equals the specified value.
    /// </summary>
    Equals,

    /// <summary>
    /// Break when hit count is greater than the specified value.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Break when hit count is a multiple of the specified value.
    /// </summary>
    MultipleOf
}

/// <summary>
/// Represents a frame in the call stack.
/// </summary>
public class StackFrame
{
    /// <summary>
    /// Gets or sets the name of the method or function.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source location.
    /// </summary>
    public SourceLocation? Location { get; set; }

    /// <summary>
    /// Gets or sets the local variables in this frame.
    /// </summary>
    public Dictionary<string, object> LocalVariables { get; set; } = new();

    /// <summary>
    /// Gets or sets the parameters for this frame.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets additional metadata for this frame.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a source code location.
/// </summary>
public class SourceLocation
{
    /// <summary>
    /// Gets or sets the line number.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the column number.
    /// </summary>
    public int ColumnNumber { get; set; }

    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the source text at this location.
    /// </summary>
    public string? SourceText { get; set; }
}

/// <summary>
/// Result of debug execution.
/// </summary>
public class DebugExecutionResult : CodeExecutionResult
{
    /// <summary>
    /// Gets the debug session information.
    /// </summary>
    public DebugSessionInfo SessionInfo { get; }

    /// <summary>
    /// Gets the breakpoints that were hit during execution.
    /// </summary>
    public IReadOnlyList<BreakpointHit> BreakpointsHit { get; }

    /// <summary>
    /// Gets the execution trace.
    /// </summary>
    public IReadOnlyList<ExecutionTraceEntry> ExecutionTrace { get; }

    /// <summary>
    /// Gets variable inspection results.
    /// </summary>
    public IReadOnlyDictionary<string, object> VariableInspections { get; }

    private DebugExecutionResult(
        bool success,
        object? output,
        string? errorMessage,
        Exception? exception,
        TimeSpan executionTime,
        IDictionary<string, object>? metadata,
        DebugSessionInfo sessionInfo,
        IReadOnlyList<BreakpointHit> breakpointsHit,
        IReadOnlyList<ExecutionTraceEntry> executionTrace,
        IReadOnlyDictionary<string, object> variableInspections)
        : base(success, output, errorMessage, exception, executionTime, metadata)
    {
        SessionInfo = sessionInfo;
        BreakpointsHit = breakpointsHit;
        ExecutionTrace = executionTrace;
        VariableInspections = variableInspections;
    }

    /// <summary>
    /// Creates a successful debug execution result.
    /// </summary>
    public static DebugExecutionResult CreateDebugSuccess(
        object? output = null,
        TimeSpan? executionTime = null,
        IDictionary<string, object>? metadata = null,
        DebugSessionInfo? sessionInfo = null,
        IReadOnlyList<BreakpointHit>? breakpointsHit = null,
        IReadOnlyList<ExecutionTraceEntry>? executionTrace = null,
        IReadOnlyDictionary<string, object>? variableInspections = null) => new DebugExecutionResult(
            true,
            output,
            null,
            null,
            executionTime ?? TimeSpan.Zero,
            metadata,
            sessionInfo ?? new DebugSessionInfo(),
            breakpointsHit ?? Array.Empty<BreakpointHit>(),
            executionTrace ?? Array.Empty<ExecutionTraceEntry>(),
            variableInspections ?? new Dictionary<string, object>());

    /// <summary>
    /// Creates a failed debug execution result.
    /// </summary>
    public static DebugExecutionResult CreateDebugFailure(
        string? errorMessage = null,
        Exception? exception = null,
        TimeSpan? executionTime = null,
        IDictionary<string, object>? metadata = null,
        DebugSessionInfo? sessionInfo = null,
        IReadOnlyList<BreakpointHit>? breakpointsHit = null,
        IReadOnlyList<ExecutionTraceEntry>? executionTrace = null) => new DebugExecutionResult(
            false,
            null,
            errorMessage,
            exception,
            executionTime ?? TimeSpan.Zero,
            metadata,
            sessionInfo ?? new DebugSessionInfo(),
            breakpointsHit ?? Array.Empty<BreakpointHit>(),
            executionTrace ?? Array.Empty<ExecutionTraceEntry>(),
            new Dictionary<string, object>());
}

/// <summary>
/// Information about a debug session.
/// </summary>
public class DebugSessionInfo
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the session start time.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the session end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the final session state.
    /// </summary>
    public DebugSessionState FinalState { get; set; }

    /// <summary>
    /// Gets or sets the number of breakpoints hit.
    /// </summary>
    public int BreakpointsHitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of steps taken.
    /// </summary>
    public int StepsTaken { get; set; }
}

/// <summary>
/// Represents a breakpoint hit event.
/// </summary>
public class BreakpointHit
{
    /// <summary>
    /// Gets or sets the breakpoint that was hit.
    /// </summary>
    public Breakpoint Breakpoint { get; set; } = new();

    /// <summary>
    /// Gets or sets when the breakpoint was hit.
    /// </summary>
    public DateTime HitTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the execution context at the time of the hit.
    /// </summary>
    public Dictionary<string, object> ExecutionContext { get; set; } = new();

    /// <summary>
    /// Gets or sets the call stack at the time of the hit.
    /// </summary>
    public List<StackFrame> CallStack { get; set; } = new();
}

/// <summary>
/// Represents an entry in the execution trace.
/// </summary>
public class ExecutionTraceEntry
{
    /// <summary>
    /// Gets or sets the timestamp of this trace entry.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the type of trace event.
    /// </summary>
    public TraceEventType EventType { get; set; }

    /// <summary>
    /// Gets or sets the source location.
    /// </summary>
    public SourceLocation? Location { get; set; }

    /// <summary>
    /// Gets or sets the event description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional event data.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Types of debug session states.
/// </summary>
public enum DebugSessionState
{
    /// <summary>
    /// Session is not started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Session is running.
    /// </summary>
    Running,

    /// <summary>
    /// Session is paused at a breakpoint.
    /// </summary>
    Paused,

    /// <summary>
    /// Session is stepping through code.
    /// </summary>
    Stepping,

    /// <summary>
    /// Session has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Session has failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Session has been stopped by user.
    /// </summary>
    Stopped
}

/// <summary>
/// Types of trace events.
/// </summary>
public enum TraceEventType
{
    /// <summary>
    /// Function or method entry.
    /// </summary>
    FunctionEntry,

    /// <summary>
    /// Function or method exit.
    /// </summary>
    FunctionExit,

    /// <summary>
    /// Line execution.
    /// </summary>
    LineExecution,

    /// <summary>
    /// Variable assignment.
    /// </summary>
    VariableAssignment,

    /// <summary>
    /// Exception thrown.
    /// </summary>
    ExceptionThrown,

    /// <summary>
    /// Breakpoint hit.
    /// </summary>
    BreakpointHit,

    /// <summary>
    /// Step operation.
    /// </summary>
    Step
}

/// <summary>
/// Types of debug events to log.
/// </summary>
[Flags]
public enum DebugEventTypes
{
    /// <summary>
    /// No events.
    /// </summary>
    None = 0,

    /// <summary>
    /// Breakpoint events.
    /// </summary>
    Breakpoints = 1,

    /// <summary>
    /// Step events.
    /// </summary>
    Steps = 2,

    /// <summary>
    /// Variable events.
    /// </summary>
    Variables = 4,

    /// <summary>
    /// Exception events.
    /// </summary>
    Exceptions = 8,

    /// <summary>
    /// All events.
    /// </summary>
    All = Breakpoints | Steps | Variables | Exceptions
}

/// <summary>
/// Event arguments for breakpoint hit events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the BreakpointHitEventArgs class.
/// </remarks>
/// <param name="breakpoint">The breakpoint that was hit.</param>
/// <param name="session">The debug session.</param>
public class BreakpointHitEventArgs(Breakpoint breakpoint, IDebugSession session) : EventArgs
{
    /// <summary>
    /// Gets the breakpoint that was hit.
    /// </summary>
    public Breakpoint Breakpoint { get; } = breakpoint ?? throw new ArgumentNullException(nameof(breakpoint));

    /// <summary>
    /// Gets the debug session.
    /// </summary>
    public IDebugSession Session { get; } = session ?? throw new ArgumentNullException(nameof(session));
}

/// <summary>
/// Event arguments for debug session state change events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the DebugSessionStateChangedEventArgs class.
/// </remarks>
/// <param name="previousState">The previous state.</param>
/// <param name="newState">The new state.</param>
/// <param name="session">The debug session.</param>
public class DebugSessionStateChangedEventArgs(
    DebugSessionState previousState,
    DebugSessionState newState,
    IDebugSession session) : EventArgs
{
    /// <summary>
    /// Gets the previous state.
    /// </summary>
    public DebugSessionState PreviousState { get; } = previousState;

    /// <summary>
    /// Gets the new state.
    /// </summary>
    public DebugSessionState NewState { get; } = newState;

    /// <summary>
    /// Gets the debug session.
    /// </summary>
    public IDebugSession Session { get; } = session ?? throw new ArgumentNullException(nameof(session));
}
