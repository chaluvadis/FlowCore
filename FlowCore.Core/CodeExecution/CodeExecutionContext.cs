namespace FlowCore.CodeExecution;

/// <summary>
/// Provides controlled access to workflow execution state for code blocks and guards.
/// Allows code to read from and write to the shared execution context while maintaining security.
/// </summary>
/// <remarks>
/// Initializes a new instance of the CodeExecutionContext.
/// </remarks>
/// <param name="workflowContext">The underlying workflow execution context.</param>
/// <param name="config">The code execution configuration.</param>
/// <param name="serviceProvider">The service provider for dependency injection.</param>
public class CodeExecutionContext(ExecutionContext workflowContext,CodeExecutionConfig config, IServiceProvider serviceProvider)
{
    private readonly ExecutionContext _workflowContext = workflowContext ?? throw new ArgumentNullException(nameof(workflowContext));
    private readonly CodeExecutionConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Gets data from the workflow state by key.
    /// </summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="key">The key of the state data.</param>
    /// <returns>The state data converted to the specified type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the state.</exception>
    /// <exception cref="InvalidCastException">Thrown when the data cannot be converted to the specified type.</exception>
    public T GetState<T>(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (!_workflowContext.State.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"State key '{key}' not found");

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Cannot convert state value '{value}' to type '{typeof(T)}'", ex);
        }
    }

    /// <summary>
    /// Gets data from the workflow state by key, returning default value if not found.
    /// </summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="key">The key of the state data.</param>
    /// <param name="defaultValue">The default value to return if key is not found.</param>
    /// <returns>The state data converted to the specified type, or the default value.</returns>
    public T GetState<T>(string key, T defaultValue)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (!_workflowContext.State.TryGetValue(key, out var value))
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Sets data in the workflow state.
    /// </summary>
    /// <param name="key">The key to store the data under.</param>
    /// <param name="value">The data to store.</param>
    public void SetState(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        _workflowContext.SetState(key, value);
    }

    /// <summary>
    /// Checks if a key exists in the workflow state.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    public bool ContainsState(string key)
    {
        return _workflowContext.State.ContainsKey(key);
    }

    /// <summary>
    /// Gets the input data provided to the workflow.
    /// </summary>
    /// <returns>The workflow input data.</returns>
    public object GetInput()
    {
        return _workflowContext.Input;
    }

    /// <summary>
    /// Gets the input data converted to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to convert the input to.</typeparam>
    /// <returns>The input data converted to the specified type.</returns>
    public T GetInput<T>()
    {
        try
        {
            return (T)Convert.ChangeType(_workflowContext.Input, typeof(T));
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Cannot convert input '{_workflowContext.Input}' to type '{typeof(T)}'", ex);
        }
    }

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Gets the cancellation token for the current execution.
    /// </summary>
    public CancellationToken CancellationToken => _workflowContext.CancellationToken;

    /// <summary>
    /// Gets the current block name being executed.
    /// </summary>
    public string CurrentBlockName => _workflowContext.CurrentBlockName ?? string.Empty;

    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    public string WorkflowName => _workflowContext.WorkflowName ?? string.Empty;

    /// <summary>
    /// Gets the execution ID.
    /// </summary>
    public Guid ExecutionId => _workflowContext.ExecutionId;

    /// <summary>
    /// Gets the parameters configured for this code execution.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters => _config.Parameters;

    /// <summary>
    /// Gets a parameter value by key.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <param name="key">The parameter key.</param>
    /// <returns>The parameter value converted to the specified type.</returns>
    public T GetParameter<T>(string key)
    {
        if (!_config.Parameters.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"Parameter '{key}' not found");

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Cannot convert parameter '{value}' to type '{typeof(T)}'", ex);
        }
    }

    /// <summary>
    /// Gets a parameter value by key with a default value.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">The default value if parameter is not found.</param>
    /// <returns>The parameter value or the default value.</returns>
    public T GetParameter<T>(string key, T defaultValue)
    {
        if (!_config.Parameters.TryGetValue(key, out var value))
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Logs a message to the workflow logger.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Arguments for the message format.</param>
    public void LogInfo(string message, params object[] args)
    {
        // Access logger through workflow context if available
        var logger = _workflowContext.Services.GetService(typeof(ILogger)) as ILogger;
        logger?.LogInformation(message, args);
    }

    /// <summary>
    /// Logs a warning to the workflow logger.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="args">Arguments for the message format.</param>
    public void LogWarning(string message, params object[] args)
    {
        var logger = _workflowContext.Services.GetService(typeof(ILogger)) as ILogger;
        logger?.LogWarning(message, args);
    }

    /// <summary>
    /// Logs an error to the workflow logger.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="message">The error message to log.</param>
    /// <param name="args">Arguments for the message format.</param>
    public void LogError(Exception exception, string message, params object[] args)
    {
        var logger = _workflowContext.Services.GetService(typeof(ILogger)) as ILogger;
        logger?.LogError(exception, message, args);
    }
}