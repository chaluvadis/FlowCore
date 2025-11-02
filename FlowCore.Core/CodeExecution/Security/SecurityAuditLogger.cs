namespace FlowCore.CodeExecution.Security;

/// <summary>
/// Provides comprehensive security audit logging for code execution activities.
/// Tracks security events, violations, and access patterns for compliance and monitoring.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SecurityAuditLogger.
/// </remarks>
/// <param name="logger">Optional logger for audit events.</param>
public class SecurityAuditLogger(ILogger? logger = null, int maxEventsToKeep = 10000)
{
    private readonly ConcurrentQueue<SecurityAuditEvent> _auditEvents = new();
    private readonly int _maxEventsToKeep = maxEventsToKeep;

    /// <summary>
    /// Logs a security event for auditing purposes.
    /// </summary>
    /// <param name="event">The security audit event to log.</param>
    public void LogSecurityEvent(SecurityAuditEvent @event)
    {
        try
        {
            // Add to in-memory queue
            _auditEvents.Enqueue(@event);

            // Maintain queue size limit
            while (_auditEvents.Count > _maxEventsToKeep)
            {
                _auditEvents.TryDequeue(out _);
            }

            // Log based on severity
            switch (@event.Severity)
            {
                case SecurityEventSeverity.Low:
                    logger?.LogDebug("Security Audit: {EventType} - {Description}", @event.EventType, @event.Description);
                    break;
                case SecurityEventSeverity.Medium:
                    logger?.LogInformation("Security Audit: {EventType} - {Description}", @event.EventType, @event.Description);
                    break;
                case SecurityEventSeverity.High:
                    logger?.LogWarning("Security Audit: {EventType} - {Description}", @event.EventType, @event.Description);
                    break;
                case SecurityEventSeverity.Critical:
                    logger?.LogError("Security Audit: {EventType} - {Description}", @event.EventType, @event.Description);
                    break;
            }

            // Add additional context for high-severity events
            if (@event.Severity >= SecurityEventSeverity.High)
            {
                logger?.LogInformation("Security Event Context: {@Context}", @event.Context);
            }
        }
        catch (Exception ex)
        {
            // Don't let audit logging break the application
            logger?.LogError(ex, "Error logging security audit event");
        }
    }

    /// <summary>
    /// Logs a code validation event.
    /// </summary>
    /// <param name="codeHash">Hash of the code being validated.</param>
    /// <param name="validationResult">The result of the validation.</param>
    /// <param name="executionMode">The mode of code execution.</param>
    /// <param name="context">Additional context information.</param>
    public void LogCodeValidation(string codeHash, ValidationResult validationResult, string executionMode, IDictionary<string, object>? context = null)
    {
        var @event = new SecurityAuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "CodeValidation",
            Severity = validationResult.IsValid ? SecurityEventSeverity.Low : SecurityEventSeverity.High,
            Description = validationResult.IsValid
                ? $"Code validation passed for execution mode {executionMode}"
                : $"Code validation failed for execution mode {executionMode}",
            Context = new Dictionary<string, object>
            {
                ["CodeHash"] = codeHash,
                ["ExecutionMode"] = executionMode,
                ["ValidationPassed"] = validationResult.IsValid,
                ["ErrorCount"] = validationResult.Errors.Count(),
                ["Errors"] = validationResult.Errors
            }
        };

        if (context != null)
        {
            foreach (var kvp in context)
            {
                @event.Context[kvp.Key] = kvp.Value;
            }
        }

        LogSecurityEvent(@event);
    }

    /// <summary>
    /// Logs a code execution event.
    /// </summary>
    /// <param name="executionId">Unique identifier for the execution.</param>
    /// <param name="executionMode">The mode of code execution.</param>
    /// <param name="success">Whether the execution was successful.</param>
    /// <param name="executionTime">Time taken for execution.</param>
    /// <param name="context">Additional context information.</param>
    public void LogCodeExecution(Guid executionId, string executionMode, bool success, TimeSpan executionTime, IDictionary<string, object>? context = null)
    {
        var @event = new SecurityAuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "CodeExecution",
            Severity = success ? SecurityEventSeverity.Low : SecurityEventSeverity.Medium,
            Description = success
                ? $"Code execution completed successfully in {executionTime.TotalMilliseconds}ms"
                : $"Code execution failed after {executionTime.TotalMilliseconds}ms",
            Context = new Dictionary<string, object>
            {
                ["ExecutionId"] = executionId,
                ["ExecutionMode"] = executionMode,
                ["Success"] = success,
                ["ExecutionTimeMs"] = executionTime.TotalMilliseconds
            }
        };

        if (context != null)
        {
            foreach (var kvp in context)
            {
                @event.Context[kvp.Key] = kvp.Value;
            }
        }

        LogSecurityEvent(@event);
    }

    /// <summary>
    /// Logs an assembly loading event.
    /// </summary>
    /// <param name="assemblyName">Name of the loaded assembly.</param>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <param name="success">Whether loading was successful.</param>
    /// <param name="context">Additional context information.</param>
    public void LogAssemblyLoad(string assemblyName, string assemblyPath, bool success, IDictionary<string, object>? context = null)
    {
        var @event = new SecurityAuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "AssemblyLoad",
            Severity = success ? SecurityEventSeverity.Low : SecurityEventSeverity.High,
            Description = success
                ? $"Assembly loaded successfully: {assemblyName}"
                : $"Assembly loading failed: {assemblyName}",
            Context = new Dictionary<string, object>
            {
                ["AssemblyName"] = assemblyName,
                ["AssemblyPath"] = assemblyPath,
                ["Success"] = success
            }
        };

        if (context != null)
        {
            foreach (var kvp in context)
            {
                @event.Context[kvp.Key] = kvp.Value;
            }
        }

        LogSecurityEvent(@event);
    }

    /// <summary>
    /// Logs a security violation event.
    /// </summary>
    /// <param name="violationType">Type of security violation.</param>
    /// <param name="description">Description of the violation.</param>
    /// <param name="severity">Severity of the violation.</param>
    /// <param name="context">Additional context information.</param>
    public void LogSecurityViolation(string violationType, string description, SecurityEventSeverity severity, IDictionary<string, object>? context = null)
    {
        var @event = new SecurityAuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "SecurityViolation",
            Severity = severity,
            Description = $"{violationType}: {description}",
            Context = context ?? new Dictionary<string, object>()
        };

        LogSecurityEvent(@event);
    }

    /// <summary>
    /// Gets recent audit events for analysis.
    /// </summary>
    /// <param name="maxEvents">Maximum number of events to return.</param>
    /// <param name="minSeverity">Minimum severity level to include.</param>
    /// <returns>Recent audit events matching the criteria.</returns>
    public IEnumerable<SecurityAuditEvent> GetRecentEvents(int maxEvents = 1000, SecurityEventSeverity minSeverity = SecurityEventSeverity.Low) => _auditEvents
            .Where(e => e.Severity >= minSeverity)
            .OrderByDescending(e => e.Timestamp)
            .Take(maxEvents);

    /// <summary>
    /// Gets audit events by type.
    /// </summary>
    /// <param name="eventType">Type of events to retrieve.</param>
    /// <param name="maxEvents">Maximum number of events to return.</param>
    /// <returns>Audit events of the specified type.</returns>
    public IEnumerable<SecurityAuditEvent> GetEventsByType(string eventType, int maxEvents = 1000) => _auditEvents
            .Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp)
            .Take(maxEvents);

    /// <summary>
    /// Generates a security audit report.
    /// </summary>
    /// <param name="startTime">Start time for the report period.</param>
    /// <param name="endTime">End time for the report period.</param>
    /// <returns>A security audit report with statistics and events.</returns>
    public SecurityAuditReport GenerateReport(DateTime startTime, DateTime endTime)
    {
        var eventsInPeriod = _auditEvents
            .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
            .ToList();

        return new SecurityAuditReport
        {
            ReportPeriod = new DateTimeRange(startTime, endTime),
            TotalEvents = eventsInPeriod.Count,
            EventsByType = eventsInPeriod.GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count()),
            EventsBySeverity = eventsInPeriod.GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            SecurityViolations = [.. eventsInPeriod
                .Where(e => e.EventType == "SecurityViolation")
                .Select(e => e.Description)],
            RecentEvents = [.. eventsInPeriod
                .OrderByDescending(e => e.Timestamp)
                .Take(100)]
        };
    }
}

/// <summary>
/// Represents a security audit event.
/// </summary>
public class SecurityAuditEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the type of security event.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets the severity level of the event.
    /// </summary>
    public SecurityEventSeverity Severity { get; set; } = SecurityEventSeverity.Low;

    /// <summary>
    /// Gets the description of the security event.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets additional context information for the event.
    /// </summary>
    public IDictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Severity levels for security events.
/// </summary>
public enum SecurityEventSeverity
{
    /// <summary>
    /// Low severity - informational events.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity - warnings and minor issues.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity - security violations and errors.
    /// </summary>
    High,

    /// <summary>
    /// Critical severity - serious security breaches.
    /// </summary>
    Critical
}

/// <summary>
/// Represents a date time range.
/// </summary>
public class DateTimeRange(DateTime start, DateTime end)
{
    public DateTime Start { get; } = start;
    public DateTime End { get; } = end;
}

/// <summary>
/// Security audit report containing statistics and events.
/// </summary>
public class SecurityAuditReport
{
    public DateTimeRange ReportPeriod { get; set; } = new DateTimeRange(DateTime.UtcNow, DateTime.UtcNow);
    public int TotalEvents { get; set; }
    public IDictionary<string, int> EventsByType { get; set; } = new Dictionary<string, int>();
    public IDictionary<SecurityEventSeverity, int> EventsBySeverity { get; set; } = new Dictionary<SecurityEventSeverity, int>();
    public IReadOnlyList<string> SecurityViolations { get; set; } = [];
    public IReadOnlyList<SecurityAuditEvent> RecentEvents { get; set; } = [];
}
