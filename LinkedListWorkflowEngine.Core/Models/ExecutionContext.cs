namespace LinkedListWorkflowEngine.Core.Models;
public class ExecutionContext(
    object input,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken = default,
    string workflowName = "")
{
    private readonly IDictionary<string, object> _state = new Dictionary<string, object>();
    private readonly IServiceProvider _serviceProvider
        = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    public object Input { get; } = input ?? throw new ArgumentNullException(nameof(input));
    public IReadOnlyDictionary<string, object> State => (IReadOnlyDictionary<string, object>)_state;
    public IServiceProvider ServiceProvider => _serviceProvider;
    public CancellationToken CancellationToken => cancellationToken;
    public Guid ExecutionId { get; } = Guid.NewGuid();
    public string WorkflowName { get; } = workflowName;
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public string CurrentBlockName { get; internal set; } = string.Empty;
    public T GetState<T>(string key, T defaultValue = default!)
    {
        if (_state.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }
    public void SetState<T>(string key, T value) => _state[key] = value!;
    public bool RemoveState(string key) => _state.Remove(key);
    public bool ContainsState(string key) => _state.ContainsKey(key);
    public T GetService<T>() where T : notnull
        => _serviceProvider.GetService<T>()
            ?? throw new InvalidOperationException($"Service of type {typeof(T)} is not registered.");
    public T? GetServiceOrDefault<T>() where T : class => _serviceProvider.GetService<T>();
    public ExecutionContext WithInput(object newInput) => new(
            newInput,
            _serviceProvider,
            cancellationToken,
            WorkflowName)
    {
        CurrentBlockName = CurrentBlockName
    };
    public ExecutionContext WithCancellationToken(CancellationToken cancellationToken) => new(
            Input,
            _serviceProvider,
            cancellationToken,
            WorkflowName)
    {
        CurrentBlockName = CurrentBlockName
    };
    public void ThrowIfCancellationRequested()
        => cancellationToken.ThrowIfCancellationRequested();
    public IDictionary<string, object> CreateStateSnapshot()
        => new Dictionary<string, object>(_state);
    public void RestoreStateSnapshot(IDictionary<string, object> snapshot)
    {
        _state.Clear();
        foreach (var kvp in snapshot)
        {
            _state[kvp.Key] = kvp.Value;
        }
    }
}