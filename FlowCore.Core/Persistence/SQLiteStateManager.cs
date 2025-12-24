namespace FlowCore.Persistence;

using Microsoft.Data.Sqlite;
using System.Text;

/// <summary>
/// SQLite-based implementation of IStateManager for persistent state storage.
/// Suitable for production use with long-running workflows and fault-tolerant execution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SQLiteStateManager class.
/// </remarks>
/// <param name="connectionString">SQLite connection string or database file path.</param>
/// <param name="config">Configuration for the state manager.</param>
/// <param name="logger">Optional logger.</param>
public class SQLiteStateManager(string connectionString, StateManagerConfig? config = null, ILogger<SQLiteStateManager>? logger = null) : IStateManager
{
    private readonly string _connectionString = NormalizeConnectionString(connectionString);
    private readonly StateManagerConfig _config = config ?? new StateManagerConfig();
    private readonly ILogger<SQLiteStateManager>? _logger = logger;
    private bool _disposed;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Ensures the database schema is initialized.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // Create tables
            var createTablesCommand = connection.CreateCommand();
            createTablesCommand.CommandText = """
                CREATE TABLE IF NOT EXISTS WorkflowStates (
                    WorkflowId TEXT NOT NULL,
                    ExecutionId TEXT NOT NULL,
                    StateData BLOB NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    CurrentBlockName TEXT,
                    StateSize INTEGER NOT NULL,
                    WorkflowVersion TEXT NOT NULL,
                    CustomMetadata TEXT,
                    PRIMARY KEY (WorkflowId, ExecutionId)
                );

                CREATE INDEX IF NOT EXISTS idx_workflow_states_workflow_id 
                    ON WorkflowStates(WorkflowId);
                
                CREATE INDEX IF NOT EXISTS idx_workflow_states_created_at 
                    ON WorkflowStates(CreatedAt);
                
                CREATE INDEX IF NOT EXISTS idx_workflow_states_status 
                    ON WorkflowStates(Status);
                """;

            await createTablesCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

            _initialized = true;
            _logger?.LogInformation("SQLite state manager initialized with database at {ConnectionString}", _connectionString);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Normalizes the connection string to ensure it's a valid SQLite connection string.
    /// </summary>
    private static string NormalizeConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        // If it looks like a simple file path, convert it to a connection string
        if (!connectionString.Contains('=', StringComparison.OrdinalIgnoreCase))
        {
            return $"Data Source={connectionString}";
        }

        return connectionString;
    }

    /// <summary>
    /// Saves the workflow state with the specified identifier.
    /// </summary>
    public async Task SaveStateAsync(
        string workflowId,
        Guid executionId,
        IDictionary<string, object> state,
        WorkflowStateMetadata? metadata = null)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        var serializedState = await SerializeStateAsync(state).ConfigureAwait(false);
        var actualMetadata = metadata ?? CreateMetadata(workflowId, executionId, WorkflowStatus.Running, serializedState.Length);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO WorkflowStates 
                (WorkflowId, ExecutionId, StateData, CreatedAt, UpdatedAt, Status, CurrentBlockName, StateSize, WorkflowVersion, CustomMetadata)
            VALUES 
                (@WorkflowId, @ExecutionId, @StateData, @CreatedAt, @UpdatedAt, @Status, @CurrentBlockName, @StateSize, @WorkflowVersion, @CustomMetadata)
            """;

        command.Parameters.AddWithValue("@WorkflowId", workflowId);
        command.Parameters.AddWithValue("@ExecutionId", executionId.ToString());
        command.Parameters.AddWithValue("@StateData", serializedState);
        command.Parameters.AddWithValue("@CreatedAt", actualMetadata.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@Status", (int)actualMetadata.Status);
        command.Parameters.AddWithValue("@CurrentBlockName", (object?)actualMetadata.CurrentBlockName ?? DBNull.Value);
        command.Parameters.AddWithValue("@StateSize", serializedState.Length);
        command.Parameters.AddWithValue("@WorkflowVersion", actualMetadata.WorkflowVersion);
        command.Parameters.AddWithValue("@CustomMetadata", SerializeMetadata(actualMetadata.CustomMetadata));

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        _logger?.LogDebug("Saved state for workflow {WorkflowId}, execution {ExecutionId} ({StateSize} bytes)",
            workflowId, executionId, serializedState.Length);
    }

    /// <summary>
    /// Loads the workflow state for the specified identifier.
    /// </summary>
    public async Task<IDictionary<string, object>?> LoadStateAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT StateData FROM WorkflowStates 
            WHERE WorkflowId = @WorkflowId AND ExecutionId = @ExecutionId
            """;

        command.Parameters.AddWithValue("@WorkflowId", workflowId);
        command.Parameters.AddWithValue("@ExecutionId", executionId.ToString());

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);

        if (result is not byte[] stateData)
        {
            _logger?.LogDebug("State not found for workflow {WorkflowId}, execution {ExecutionId}",
                workflowId, executionId);
            return null;
        }

        var state = await DeserializeStateAsync(stateData).ConfigureAwait(false);
        _logger?.LogDebug("Loaded state for workflow {WorkflowId}, execution {ExecutionId} ({StateSize} bytes)",
            workflowId, executionId, stateData.Length);

        return state;
    }

    /// <summary>
    /// Deletes the workflow state for the specified identifier.
    /// </summary>
    public async Task DeleteStateAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM WorkflowStates 
            WHERE WorkflowId = @WorkflowId AND ExecutionId = @ExecutionId
            """;

        command.Parameters.AddWithValue("@WorkflowId", workflowId);
        command.Parameters.AddWithValue("@ExecutionId", executionId.ToString());

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        _logger?.LogDebug("Deleted state for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);
    }

    /// <summary>
    /// Checks if state exists for the specified identifier.
    /// </summary>
    public async Task<bool> StateExistsAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM WorkflowStates 
            WHERE WorkflowId = @WorkflowId AND ExecutionId = @ExecutionId
            """;

        command.Parameters.AddWithValue("@WorkflowId", workflowId);
        command.Parameters.AddWithValue("@ExecutionId", executionId.ToString());

        var count = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
        return count > 0;
    }

    /// <summary>
    /// Gets all execution IDs for a specific workflow.
    /// </summary>
    public async Task<IEnumerable<Guid>> GetExecutionIdsAsync(string workflowId)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ExecutionId FROM WorkflowStates 
            WHERE WorkflowId = @WorkflowId
            ORDER BY CreatedAt DESC
            """;

        command.Parameters.AddWithValue("@WorkflowId", workflowId);

        var executionIds = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (Guid.TryParse(reader.GetString(0), out var executionId))
            {
                executionIds.Add(executionId);
            }
        }

        return executionIds;
    }

    /// <summary>
    /// Gets the metadata for a specific workflow execution.
    /// </summary>
    public async Task<WorkflowStateMetadata?> GetStateMetadataAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Status, CurrentBlockName, StateSize, WorkflowVersion, CustomMetadata, CreatedAt, UpdatedAt
            FROM WorkflowStates 
            WHERE WorkflowId = @WorkflowId AND ExecutionId = @ExecutionId
            """;

        command.Parameters.AddWithValue("@WorkflowId", workflowId);
        command.Parameters.AddWithValue("@ExecutionId", executionId.ToString());

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return null;
        }

        var status = (WorkflowStatus)reader.GetInt32(0);
        var currentBlockName = reader.IsDBNull(1) ? null : reader.GetString(1);
        var stateSize = reader.GetInt64(2);
        var workflowVersion = reader.GetString(3);
        var customMetadataJson = reader.IsDBNull(4) ? null : reader.GetString(4);
        var customMetadata = DeserializeMetadata(customMetadataJson);

        var metadata = new WorkflowStateMetadata(
            workflowId,
            executionId,
            status,
            currentBlockName,
            stateSize,
            workflowVersion,
            customMetadata);

        return metadata;
    }

    /// <summary>
    /// Updates the metadata for a workflow execution.
    /// </summary>
    public async Task UpdateStateMetadataAsync(string workflowId, Guid executionId, WorkflowStateMetadata metadata)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE WorkflowStates 
            SET Status = @Status, 
                CurrentBlockName = @CurrentBlockName, 
                WorkflowVersion = @WorkflowVersion,
                CustomMetadata = @CustomMetadata,
                UpdatedAt = @UpdatedAt
            WHERE WorkflowId = @WorkflowId AND ExecutionId = @ExecutionId
            """;

        command.Parameters.AddWithValue("@Status", (int)metadata.Status);
        command.Parameters.AddWithValue("@CurrentBlockName", (object?)metadata.CurrentBlockName ?? DBNull.Value);
        command.Parameters.AddWithValue("@WorkflowVersion", metadata.WorkflowVersion);
        command.Parameters.AddWithValue("@CustomMetadata", SerializeMetadata(metadata.CustomMetadata));
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@WorkflowId", workflowId);
        command.Parameters.AddWithValue("@ExecutionId", executionId.ToString());

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Cleans up old workflow states based on the specified criteria.
    /// </summary>
    public async Task<int> CleanupOldStatesAsync(DateTime olderThan, string? workflowId = null, WorkflowStatus? status = null)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var query = new StringBuilder("DELETE FROM WorkflowStates WHERE CreatedAt < @OlderThan");
        
        if (workflowId != null)
        {
            query.Append(" AND WorkflowId = @WorkflowId");
        }
        
        if (status.HasValue)
        {
            query.Append(" AND Status = @Status");
        }

        var command = connection.CreateCommand();
        command.CommandText = query.ToString();
        command.Parameters.AddWithValue("@OlderThan", olderThan.ToString("O"));
        
        if (workflowId != null)
        {
            command.Parameters.AddWithValue("@WorkflowId", workflowId);
        }
        
        if (status.HasValue)
        {
            command.Parameters.AddWithValue("@Status", (int)status.Value);
        }

        var deletedCount = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        _logger?.LogInformation("Cleaned up {DeletedCount} old workflow states", deletedCount);
        return deletedCount;
    }

    /// <summary>
    /// Gets statistics about stored workflow states.
    /// </summary>
    public async Task<StateManagerStatistics> GetStatisticsAsync()
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 
                COUNT(*) as TotalStates,
                SUM(StateSize) as TotalSize,
                SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) as ActiveExecutions,
                SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) as CompletedExecutions,
                SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) as FailedExecutions
            FROM WorkflowStates
            """;

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        var stats = new StateManagerStatistics();

        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            stats.TotalStates = reader.GetInt64(0);
            stats.TotalSizeBytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            stats.ActiveExecutions = reader.GetInt64(2);
            stats.CompletedExecutions = reader.GetInt64(3);
            stats.FailedExecutions = reader.GetInt64(4);
        }

        stats.GeneratedAt = DateTime.UtcNow;
        return stats;
    }

    /// <summary>
    /// Disposes of the state manager and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _initLock.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Serializes state data for storage.
    /// </summary>
    private async Task<byte[]> SerializeStateAsync(IDictionary<string, object> state)
    {
        var serializer = new WorkflowStateSerializer(_config, _logger);
        return await serializer.SerializeAsync(state).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes state data from storage.
    /// </summary>
    private async Task<IDictionary<string, object>?> DeserializeStateAsync(byte[] data)
    {
        var serializer = new WorkflowStateSerializer(_config, _logger);
        return await serializer.DeserializeAsync(data).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes metadata to JSON string.
    /// </summary>
    private static string SerializeMetadata(IDictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(metadata);
    }

    /// <summary>
    /// Deserializes metadata from JSON string.
    /// </summary>
    private static IDictionary<string, object> DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson) 
                ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Creates default metadata for a workflow execution.
    /// </summary>
    private static WorkflowStateMetadata CreateMetadata(string workflowId, Guid executionId, WorkflowStatus status, long stateSize) => new(
            workflowId,
            executionId,
            status,
            stateSize: stateSize,
            workflowVersion: "1.0.0");

    /// <summary>
    /// Throws an ObjectDisposedException if the state manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SQLiteStateManager));
    }
}
