namespace FlowCore.CodeExecution.Debugging;

/// <summary>
/// Basic implementation of code execution debugger.
/// Provides simplified debugging capabilities for code execution troubleshooting.
/// </summary>
/// <remarks>
/// Initializes a new instance of the BasicCodeExecutionDebugger.
/// </remarks>
/// <param name="logger">Optional logger for debug operations.</param>
public class BasicCodeExecutionDebugger(ILogger? logger = null) : ICodeExecutionDebugger
{
    private readonly ConcurrentDictionary<string, Breakpoint> _breakpoints = new();
    private readonly object _sessionLock = new();

    /// <summary>
    /// Gets a value indicating whether debugging is currently enabled.
    /// </summary>
    public bool IsDebuggingEnabled => CurrentSession != null;

    /// <summary>
    /// Gets the current debugging session, if any.
    /// </summary>
    public IDebugSession? CurrentSession { get; private set; }

    /// <summary>
    /// Starts a new debugging session.
    /// </summary>
    /// <param name="config">Configuration for the debugging session.</param>
    /// <returns>The created debug session.</returns>
    public async Task<IDebugSession> StartDebugSessionAsync(DebugConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_sessionLock)
        {
            if (CurrentSession != null)
            {
                throw new InvalidOperationException("A debug session is already active");
            }

            CurrentSession = new BasicDebugSession(config, logger);

            logger?.LogDebug("Started debug session {SessionId}", CurrentSession.SessionId);
        }

        await Task.CompletedTask.ConfigureAwait(false); // Make method async
        return CurrentSession;
    }

    /// <summary>
    /// Stops the current debugging session.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    public async Task StopDebugSessionAsync()
    {
        lock (_sessionLock)
        {
            if (CurrentSession != null)
            {
                logger?.LogDebug("Stopping debug session {SessionId}", CurrentSession.SessionId);

                CurrentSession.Dispose();
                CurrentSession = null;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false); // Make method async
    }

    /// <summary>
    /// Sets a breakpoint at the specified location.
    /// </summary>
    /// <param name="breakpoint">The breakpoint to set.</param>
    /// <returns>True if the breakpoint was set successfully.</returns>
    public async Task<bool> SetBreakpointAsync(Breakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        try
        {
            _breakpoints.AddOrUpdate(breakpoint.Id, breakpoint, (_, _) => breakpoint);
            logger?.LogDebug("Set breakpoint {BreakpointId} at line {LineNumber}", breakpoint.Id, breakpoint.LineNumber);

            await Task.CompletedTask.ConfigureAwait(false); // Make method async
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to set breakpoint {BreakpointId}", breakpoint.Id);
            return false;
        }
    }

    /// <summary>
    /// Removes a breakpoint.
    /// </summary>
    /// <param name="breakpointId">The ID of the breakpoint to remove.</param>
    /// <returns>True if the breakpoint was removed successfully.</returns>
    public async Task<bool> RemoveBreakpointAsync(string breakpointId)
    {
        if (string.IsNullOrEmpty(breakpointId))
        {
            return false;
        }

        try
        {
            var removed = _breakpoints.TryRemove(breakpointId, out _);
            if (removed)
            {
                logger?.LogDebug("Removed breakpoint {BreakpointId}", breakpointId);
            }

            await Task.CompletedTask.ConfigureAwait(false); // Make method async
            return removed;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to remove breakpoint {BreakpointId}", breakpointId);
            return false;
        }
    }

    /// <summary>
    /// Gets all active breakpoints.
    /// </summary>
    /// <returns>A list of active breakpoints.</returns>
    public async Task<IEnumerable<Breakpoint>> GetBreakpointsAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false); // Make method async
        return [.. _breakpoints.Values.Where(bp => bp.IsEnabled)];
    }

    /// <summary>
    /// Executes code with debugging support.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The debug execution result.</returns>
    public async Task<DebugExecutionResult> ExecuteWithDebuggingAsync(
        CodeExecutionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startTime = DateTime.UtcNow;
        var session = CurrentSession as BasicDebugSession;

        if (session == null)
        {
            throw new InvalidOperationException("No active debug session");
        }

        try
        {
            logger?.LogDebug("Starting debug execution for context {ExecutionId}", context.ExecutionId);

            // Set up the session context
            await session.SetContextAsync(context).ConfigureAwait(false);

            // Execute with debugging
            var result = await ExecuteWithDebugSupportAsync(context, session, ct).ConfigureAwait(false);

            var executionTime = DateTime.UtcNow - startTime;
            var sessionInfo = new DebugSessionInfo
            {
                SessionId = session.SessionId,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                FinalState = session.State,
                BreakpointsHitCount = session.GetBreakpointsHit().Count,
                StepsTaken = session.GetStepsTaken()
            };

            logger?.LogDebug("Debug execution completed for context {ExecutionId} in {ExecutionTime}",
                context.ExecutionId, executionTime);

            return DebugExecutionResult.CreateDebugSuccess(
                result.Output,
                executionTime,
                new Dictionary<string, object> { ["DebugMode"] = true },
                sessionInfo,
                session.GetBreakpointsHit(),
                session.GetExecutionTrace(),
                session.GetVariableInspections());
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Debug execution failed for context {ExecutionId}", context.ExecutionId);

            var executionTime = DateTime.UtcNow - startTime;
            var sessionInfo = new DebugSessionInfo
            {
                SessionId = session?.SessionId ?? "unknown",
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                FinalState = DebugSessionState.Failed
            };

            return DebugExecutionResult.CreateDebugFailure(
                ex.Message,
                ex,
                executionTime,
                sessionInfo: sessionInfo);
        }
    }

    private async Task<ExecutionResult> ExecuteWithDebugSupportAsync(
        CodeExecutionContext context,
        BasicDebugSession session,
        CancellationToken ct)
    {
        // Simulate code execution with debug support
        session.AddTraceEntry(TraceEventType.FunctionEntry, "Code execution started");

        // Check for breakpoints on first line if configured
        if (session.Configuration.BreakOnFirstLine)
        {
            var firstLineBreakpoint = new Breakpoint { LineNumber = 1, Id = "first-line" };
            await HandleBreakpointHitAsync(session, firstLineBreakpoint).ConfigureAwait(false);
        }

        try
        {
            // Simulate stepping through code
            for (int line = 1; line <= 10; line++) // Simulate 10 lines of code
            {
                ct.ThrowIfCancellationRequested();

                session.AddTraceEntry(TraceEventType.LineExecution, $"Executing line {line}");

                // Check for breakpoints at this line
                var breakpoint = _breakpoints.Values.FirstOrDefault(bp =>
                    bp.IsEnabled && bp.LineNumber == line && ShouldBreakpointTrigger(bp));

                if (breakpoint != null)
                {
                    await HandleBreakpointHitAsync(session, breakpoint).ConfigureAwait(false);
                }

                // Simulate variable changes
                if (line % 3 == 0)
                {
                    var variableName = $"var{line}";
                    var variableValue = $"value{line}";
                    session.SetVariable(variableName, variableValue);
                    session.AddTraceEntry(TraceEventType.VariableAssignment,
                        $"Set {variableName} = {variableValue}");
                }

                // Small delay to simulate execution time
                await Task.Delay(10, ct).ConfigureAwait(false);
            }

            session.AddTraceEntry(TraceEventType.FunctionExit, "Code execution completed");
            return new ExecutionResult(true, "Debug execution completed", null);
        }
        catch (Exception ex)
        {
            session.AddTraceEntry(TraceEventType.ExceptionThrown, $"Exception: {ex.Message}");

            if (session.Configuration.BreakOnExceptions)
            {
                var exceptionBreakpoint = new Breakpoint
                {
                    Id = "exception",
                    LineNumber = -1,
                    Metadata = { ["Exception"] = ex }
                };
                await HandleBreakpointHitAsync(session, exceptionBreakpoint).ConfigureAwait(false);
            }

            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }

    private static async Task HandleBreakpointHitAsync(BasicDebugSession session, Breakpoint breakpoint)
    {
        breakpoint.HitCount++;
        session.OnBreakpointHit(breakpoint);

        // Wait for user action (continue, step, etc.)
        await session.WaitForUserActionAsync().ConfigureAwait(false);
    }

    private static bool ShouldBreakpointTrigger(Breakpoint breakpoint)
    {
        if (breakpoint.HitCountCondition == null)
        {
            return true;
        }

        return breakpoint.HitCountCondition.Type switch
        {
            HitCountConditionType.Equals => breakpoint.HitCount + 1 == breakpoint.HitCountCondition.Value,
            HitCountConditionType.GreaterThan => breakpoint.HitCount + 1 > breakpoint.HitCountCondition.Value,
            HitCountConditionType.MultipleOf => (breakpoint.HitCount + 1) % breakpoint.HitCountCondition.Value == 0,
            _ => true
        };
    }

    sealed class ExecutionResult(bool success, object? output, string? errorMessage, Exception? exception = null)
    {
        public bool Success { get; } = success;
        public object? Output { get; } = output;
        public string? ErrorMessage { get; } = errorMessage;
        public Exception? Exception { get; } = exception;
    }
}

/// <summary>
/// Basic implementation of debug session.
/// </summary>
sealed class BasicDebugSession : IDebugSession
{
    private readonly ILogger? _logger;
    private readonly List<BreakpointHit> _breakpointsHit = new();
    private readonly List<ExecutionTraceEntry> _executionTrace = new();
    private readonly Dictionary<string, object> _variables = new();
    private readonly List<StackFrame> _callStack = new();
    private readonly TaskCompletionSource<bool> _userActionCompletionSource = new();
    private int _stepsTaken;

    /// <summary>
    /// Gets the unique identifier for this debug session.
    /// </summary>
    public string SessionId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the debug configuration.
    /// </summary>
    public DebugConfiguration Configuration { get; }

    /// <summary>
    /// Gets the current execution state of the debug session.
    /// </summary>
    public DebugSessionState State
    {
        get;
        private set
        {
            var previousState = field;
            field = value;
            StateChanged?.Invoke(this, new DebugSessionStateChangedEventArgs(previousState, value, this));
        }
    } = DebugSessionState.NotStarted;

    /// <summary>
    /// Gets the current execution context being debugged.
    /// </summary>
    public CodeExecutionContext? CurrentContext { get; private set; }

    /// <summary>
    /// Gets the current call stack.
    /// </summary>
    public IReadOnlyList<StackFrame> CallStack => _callStack.AsReadOnly();

    /// <summary>
    /// Gets the current variables in scope.
    /// </summary>
    public IReadOnlyDictionary<string, object> Variables => _variables.AsReadOnly();

    /// <summary>
    /// Event fired when execution hits a breakpoint.
    /// </summary>
    public event EventHandler<BreakpointHitEventArgs>? BreakpointHit;

    /// <summary>
    /// Event fired when the debug session state changes.
    /// </summary>
    public event EventHandler<DebugSessionStateChangedEventArgs>? StateChanged;

    public BasicDebugSession(DebugConfiguration config, ILogger? logger = null)
    {
        Configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        State = DebugSessionState.Running;
    }

    /// <summary>
    /// Continues execution until the next breakpoint or completion.
    /// </summary>
    /// <returns>A task representing the continue operation.</returns>
    public async Task ContinueAsync()
    {
        _logger?.LogDebug("Continue requested for debug session {SessionId}", SessionId);
        State = DebugSessionState.Running;
        _userActionCompletionSource.TrySetResult(true);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Steps to the next line of code.
    /// </summary>
    /// <returns>A task representing the step operation.</returns>
    public async Task StepOverAsync()
    {
        _logger?.LogDebug("Step over requested for debug session {SessionId}", SessionId);
        State = DebugSessionState.Stepping;
        _stepsTaken++;
        AddTraceEntry(TraceEventType.Step, "Step over");
        _userActionCompletionSource.TrySetResult(true);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Steps into the next function call.
    /// </summary>
    /// <returns>A task representing the step operation.</returns>
    public async Task StepIntoAsync()
    {
        _logger?.LogDebug("Step into requested for debug session {SessionId}", SessionId);
        State = DebugSessionState.Stepping;
        _stepsTaken++;
        AddTraceEntry(TraceEventType.Step, "Step into");
        _userActionCompletionSource.TrySetResult(true);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Steps out of the current function.
    /// </summary>
    /// <returns>A task representing the step operation.</returns>
    public async Task StepOutAsync()
    {
        _logger?.LogDebug("Step out requested for debug session {SessionId}", SessionId);
        State = DebugSessionState.Stepping;
        _stepsTaken++;
        AddTraceEntry(TraceEventType.Step, "Step out");
        _userActionCompletionSource.TrySetResult(true);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates an expression in the current context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The result of the expression evaluation.</returns>
    public async Task<object?> EvaluateExpressionAsync(string expression)
    {
        _logger?.LogDebug("Evaluating expression '{Expression}' in debug session {SessionId}", expression, SessionId);

        // Simplified expression evaluation
        if (_variables.TryGetValue(expression, out var value))
        {
            return value;
        }

        // For demonstration, return a placeholder result
        await Task.CompletedTask.ConfigureAwait(false);
        return $"Evaluated: {expression}";
    }

    /// <summary>
    /// Gets the value of a variable.
    /// </summary>
    /// <param name="variableName">The name of the variable.</param>
    /// <returns>The variable value.</returns>
    public async Task<object?> GetVariableValueAsync(string variableName)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return _variables.TryGetValue(variableName, out var value) ? value : null;
    }

    /// <summary>
    /// Sets the value of a variable.
    /// </summary>
    /// <param name="variableName">The name of the variable.</param>
    /// <param name="value">The new value.</param>
    /// <returns>True if the variable was set successfully.</returns>
    public async Task<bool> SetVariableValueAsync(string variableName, object? value)
    {
        try
        {
            if (value == null)
            {
                _variables.Remove(variableName);
            }
            else
            {
                _variables[variableName] = value;
            }

            _logger?.LogDebug("Set variable '{VariableName}' = '{Value}' in debug session {SessionId}",
                variableName, value, SessionId);

            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set variable '{VariableName}' in debug session {SessionId}",
                variableName, SessionId);
            return false;
        }
    }

    internal async Task SetContextAsync(CodeExecutionContext context)
    {
        CurrentContext = context;

        // Initialize call stack
        _callStack.Clear();
        _callStack.Add(new StackFrame
        {
            MethodName = "Execute",
            Location = new SourceLocation { LineNumber = 1, ColumnNumber = 1 }
        });

        // Enforce max call stack depth
        while (_callStack.Count > Configuration.MaxCallStackDepth)
        {
            _callStack.RemoveAt(0);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    internal void OnBreakpointHit(Breakpoint breakpoint)
    {
        State = DebugSessionState.Paused;

        var breakpointHit = new BreakpointHit
        {
            Breakpoint = breakpoint,
            HitTime = DateTime.UtcNow,
            ExecutionContext = new Dictionary<string, object>(_variables),
            CallStack = new List<StackFrame>(_callStack)
        };

        _breakpointsHit.Add(breakpointHit);
        BreakpointHit?.Invoke(this, new BreakpointHitEventArgs(breakpoint, this));
    }

    internal async Task WaitForUserActionAsync() => await _userActionCompletionSource.Task.ConfigureAwait(false);

    internal void AddTraceEntry(TraceEventType eventType, string description) => _executionTrace.Add(new ExecutionTraceEntry
    {
        EventType = eventType,
        Description = description,
        Location = new SourceLocation { LineNumber = _executionTrace.Count + 1 }
    });

    internal void SetVariable(string name, object value)
    {
        // Enforce max variables limit
        if (_variables.Count >= Configuration.MaxVariablesPerScope)
        {
            // Evict oldest variable (simple FIFO)
            var oldest = _variables.Keys.First();
            _variables.Remove(oldest);
            _logger?.LogDebug("Evicted variable '{VariableName}' due to limit", oldest);
        }
        _variables[name] = value;
    }

    internal IReadOnlyList<BreakpointHit> GetBreakpointsHit() => _breakpointsHit.AsReadOnly();
    internal IReadOnlyList<ExecutionTraceEntry> GetExecutionTrace() => _executionTrace.AsReadOnly();
    internal IReadOnlyDictionary<string, object> GetVariableInspections()
    {
        // Limit variable inspections for performance
        var limitedVariables = _variables.Take(Configuration.MaxVariablesPerScope).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return limitedVariables.AsReadOnly();
    }
    internal int GetStepsTaken() => _stepsTaken;

    public void Dispose()
    {
        State = DebugSessionState.Stopped;
        _userActionCompletionSource.TrySetCanceled();
        _logger?.LogDebug("Debug session {SessionId} disposed", SessionId);
    }
}
