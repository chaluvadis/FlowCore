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
    private readonly Lock _sessionLock = new();

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

        return await Task.FromResult(CurrentSession).ConfigureAwait(false);
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
            _ = _breakpoints.AddOrUpdate(breakpoint.Id, breakpoint, (_, _) => breakpoint);

            logger?.LogDebug("Set breakpoint {BreakpointId} at line {LineNumber}", breakpoint.Id, breakpoint.LineNumber);

            return await Task.FromResult(true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to set breakpoint {BreakpointId}", breakpoint.Id);
            return await Task.FromResult(false).ConfigureAwait(false);
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

            return await Task.FromResult(removed).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to remove breakpoint {BreakpointId}", breakpointId);
            return await Task.FromResult(false).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets all active breakpoints.
    /// </summary>
    /// <returns>A list of active breakpoints.</returns>
    public async Task<IEnumerable<Breakpoint>> GetBreakpointsAsync()
    {
        return await Task.FromResult(_breakpoints.Values.Where(bp => bp.IsEnabled)).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes code with debugging support.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The debug execution result.</returns>
    public async Task<DebugExecutionResult> ExecuteWithDebuggingAsync(CodeExecutionContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startTime = DateTime.UtcNow;
        var session = CurrentSession as BasicDebugSession ?? throw new InvalidOperationException("No active debug session");
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
                StepsTaken = session.GetStepsTaken(),
            };

            logger?.LogDebug("Debug execution completed for context {ExecutionId} in {ExecutionTime}", context.ExecutionId, executionTime);

            return DebugExecutionResult.CreateDebugSuccess(
                result.Output,
                executionTime,
                new Dictionary<string, object> { ["DebugMode"] = true },
                sessionInfo,
                session.GetBreakpointsHit(),
                session.GetExecutionTrace(),
                session.GetVariableInspections()
            );
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
                FinalState = DebugSessionState.Failed,
            };

            return DebugExecutionResult.CreateDebugFailure(ex.Message, ex, executionTime, sessionInfo: sessionInfo);
        }
    }

    private async Task<ExecutionResult> ExecuteWithDebugSupportAsync(CodeExecutionContext context, BasicDebugSession session, CancellationToken ct)
    {
        session.AddTraceEntry(TraceEventType.FunctionEntry, "Code execution started");

        // Check for breakpoints on first line if configured
        if (session.Configuration.BreakOnFirstLine)
        {
            var firstLineBreakpoint = new Breakpoint { LineNumber = 1, Id = "first-line" };
            await HandleBreakpointHitAsync(session, firstLineBreakpoint).ConfigureAwait(false);
        }

        try
        {
            CodeExecutionResult result;

            if (context.Config.Mode == CodeExecutionMode.Inline)
            {
                // Execute inline code with debugging
                result = await ExecuteInlineCodeWithDebuggingAsync(context, session, ct).ConfigureAwait(false);
            }
            else
            {
                // Execute assembly-based code with debugging
                result = await ExecuteAssemblyCodeWithDebuggingAsync(context, session, ct).ConfigureAwait(false);
            }

            session.AddTraceEntry(TraceEventType.FunctionExit, "Code execution completed");
            return result.Success
                ? FlowCore.Models.ExecutionResult.Success(null, result.Output)
                : FlowCore.Models.ExecutionResult.Failure(null, result.Output, result.Exception);
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
                    Metadata = { ["Exception"] = ex },
                };
                await HandleBreakpointHitAsync(session, exceptionBreakpoint).ConfigureAwait(false);
            }

            return FlowCore.Models.ExecutionResult.Failure(null, null, ex);
        }
    }

    private async Task<CodeExecutionResult> ExecuteInlineCodeWithDebuggingAsync(CodeExecutionContext context, BasicDebugSession session, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Parse and analyze the code for debugging
            var syntaxTree = CSharpSyntaxTree.ParseText(context.Config.Code);
            var root = await syntaxTree.GetRootAsync(ct).ConfigureAwait(false);

            // Simulate line-by-line execution for debugging
            var lines = context.Config.Code.Split('\n');
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var lineNumber = lineIndex + 1;
                var line = lines[lineIndex].Trim();

                if (string.IsNullOrWhiteSpace(line)) continue;

                session.AddTraceEntry(TraceEventType.LineExecution, $"Executing line {lineNumber}: {line}");

                // Check for breakpoints at this line
                var breakpoint = _breakpoints.Values.FirstOrDefault(bp => bp.IsEnabled && bp.LineNumber == lineNumber && ShouldBreakpointTrigger(bp));
                if (breakpoint != null)
                {
                    await HandleBreakpointHitAsync(session, breakpoint).ConfigureAwait(false);
                }

                // Simulate variable inspection and changes
                if (line.Contains("SetState") || line.Contains("context.SetState"))
                {
                    // Extract variable assignments for debugging
                    var variableMatch = System.Text.RegularExpressions.Regex.Match(line, @"SetState\s*\(\s*[""']([^""']+)[""']\s*,\s*([^)]+)\)");
                    if (variableMatch.Success)
                    {
                        var varName = variableMatch.Groups[1].Value;
                        var varValue = variableMatch.Groups[2].Value;
                        session.SetVariable(varName, varValue);
                        session.AddTraceEntry(TraceEventType.VariableAssignment, $"Set {varName} = {varValue}");
                    }
                }

                // Small delay to simulate execution time
                await Task.Delay(10, ct).ConfigureAwait(false);
            }

            // Compile and execute the actual code
            var assembly = CodeCompiler.Compile(
                context.Config.Code,
                $"DynamicClass_{Guid.NewGuid():N}",
                "Execute",
                "object",
                "CodeExecutionContext");

            var result = CodeCompiler.ExecuteMethod(assembly, $"DynamicClass_{Guid.NewGuid():N}", "Execute", context);

            var executionTime = DateTime.UtcNow - startTime;
            return CodeExecutionResult.CreateSuccess(result, executionTime);
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            return CodeExecutionResult.CreateFailure(ex.Message, ex, executionTime);
        }
    }

    private async Task<CodeExecutionResult> ExecuteAssemblyCodeWithDebuggingAsync(CodeExecutionContext context, BasicDebugSession session, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Load the assembly
            var assembly = Assembly.LoadFrom(context.Config.AssemblyPath);
            var type = assembly.GetType(context.Config.TypeName);
            if (type == null)
            {
                throw new InvalidOperationException($"Type {context.Config.TypeName} not found in assembly {context.Config.AssemblyPath}");
            }

            var method = type.GetMethod(context.Config.MethodName);
            if (method == null)
            {
                throw new InvalidOperationException($"Method {context.Config.MethodName} not found in type {context.Config.TypeName}");
            }

            // Simulate method entry breakpoint
            var entryBreakpoint = new Breakpoint { LineNumber = 1, Id = "method-entry" };
            if (_breakpoints.Values.Any(bp => bp.IsEnabled && bp.LineNumber == 1))
            {
                await HandleBreakpointHitAsync(session, entryBreakpoint).ConfigureAwait(false);
            }

            session.AddTraceEntry(TraceEventType.FunctionEntry, $"Entering method {context.Config.MethodName}");

            // Execute the method
            var instance = Activator.CreateInstance(type);
            var result = method.Invoke(instance, [context]);

            session.AddTraceEntry(TraceEventType.FunctionExit, $"Exiting method {context.Config.MethodName}");

            var executionTime = DateTime.UtcNow - startTime;
            return CodeExecutionResult.CreateSuccess(result, executionTime);
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            return CodeExecutionResult.CreateFailure(ex.Message, ex, executionTime);
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
            _ => true,
        };
    }

    // Removed simulated ExecutionResult class - now using CodeExecutionResult
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

            _logger?.LogDebug("Set variable '{VariableName}' = '{Value}' in debug session {SessionId}", variableName, value, SessionId);

            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set variable '{VariableName}' in debug session {SessionId}", variableName, SessionId);
            return false;
        }
    }

    internal async Task SetContextAsync(CodeExecutionContext context)
    {
        CurrentContext = context;

        // Initialize call stack
        _callStack.Clear();
        _callStack.Add(
            new StackFrame
            {
                MethodName = "Execute",
                Location = new SourceLocation { LineNumber = 1, ColumnNumber = 1 },
            }
        );

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
            CallStack = new List<StackFrame>(_callStack),
        };

        _breakpointsHit.Add(breakpointHit);
        BreakpointHit?.Invoke(this, new BreakpointHitEventArgs(breakpoint, this));
    }

    internal async Task WaitForUserActionAsync() => await _userActionCompletionSource.Task.ConfigureAwait(false);

    internal void AddTraceEntry(TraceEventType eventType, string description) =>
        _executionTrace.Add(
            new ExecutionTraceEntry
            {
                EventType = eventType,
                Description = description,
                Location = new SourceLocation { LineNumber = _executionTrace.Count + 1 },
            }
        );

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
