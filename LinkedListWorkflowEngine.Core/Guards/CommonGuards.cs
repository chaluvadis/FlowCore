namespace LinkedListWorkflowEngine.Core.Guards;
/// <summary>
/// Common guard implementations for typical validation scenarios.
/// </summary>
public static class CommonGuards
{
    /// <summary>
    /// Guard that validates business hours.
    /// </summary>
    public class BusinessHoursGuard : IGuard
    {
        private readonly TimeSpan _startTime;
        private readonly TimeSpan _endTime;
        private readonly DayOfWeek[] _validDays;
        private readonly string[] _holidayDates;
        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public string GuardId => $"BusinessHours_{_startTime:hhmm}_{_endTime:hhmm}";
        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public string DisplayName => "Business Hours Validation";
        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public string Description => $"Validates that current time is within business hours {_startTime:hh:mm} to {_endTime:hh:mm}";
        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public GuardSeverity Severity => GuardSeverity.Error;
        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public string Category => "Business Rules";
        /// <summary>
        /// Initializes a new instance of the BusinessHoursGuard class.
        /// </summary>
        /// <param name="startTime">The start of business hours.</param>
        /// <param name="endTime">The end of business hours.</param>
        /// <param name="validDays">The valid days of the week (defaults to weekdays).</param>
        /// <param name="holidayDates">Optional holiday dates to exclude.</param>
        public BusinessHoursGuard(
            TimeSpan startTime,
            TimeSpan endTime,
            DayOfWeek[]? validDays = null,
            string[]? holidayDates = null)
        {
            _startTime = startTime;
            _endTime = endTime;
            _validDays = validDays ?? new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            _holidayDates = holidayDates ?? Array.Empty<string>();
        }
        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var now = DateTime.UtcNow;
            var contextData = new Dictionary<string, object>
            {
                ["CurrentTime"] = now,
                ["StartTime"] = _startTime,
                ["EndTime"] = _endTime,
                ["IsWeekend"] = !_validDays.Contains(now.DayOfWeek),
                ["ValidDays"] = _validDays
            };
            // Check if today is a valid day
            if (!_validDays.Contains(now.DayOfWeek))
            {
                return GuardResult.Failure(
                    $"Operation attempted outside business days. Current day: {now.DayOfWeek}",
                    severity: GuardSeverity.Warning,
                    context: contextData);
            }
            // Check if today is a holiday
            var todayString = now.ToString("yyyy-MM-dd");
            if (_holidayDates.Contains(todayString))
            {
                return GuardResult.Failure(
                    $"Operation attempted on holiday: {todayString}",
                    severity: GuardSeverity.Warning,
                    context: contextData);
            }
            // Check business hours
            var currentTime = now.TimeOfDay;
            if (currentTime < _startTime || currentTime > _endTime)
            {
                return GuardResult.Failure(
                    $"Operation attempted outside business hours. Current time: {currentTime:hh\\:mm}, Business hours: {_startTime:hh\\:mm} - {_endTime:hh\\:mm}",
                    context: contextData);
            }
            return GuardResult.Success(contextData);
        }
    }
    /// <summary>
    /// Guard that validates data format using regular expressions.
    /// </summary>
    public class DataFormatGuard : IGuard
    {
        private readonly string _fieldName;
        private readonly string _pattern;
        private readonly RegexOptions _regexOptions;
        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public string GuardId => $"DataFormat_{_fieldName}";
        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public string DisplayName => $"Data Format Validation for {_fieldName}";
        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public string Description => $"Validates that {_fieldName} matches pattern {_pattern}";
        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public GuardSeverity Severity => GuardSeverity.Error;
        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public string Category => "Data Validation";
        /// <summary>
        /// Initializes a new instance of the DataFormatGuard class.
        /// </summary>
        /// <param name="fieldName">The name of the field to validate.</param>
        /// <param name="pattern">The regular expression pattern to match against.</param>
        /// <param name="regexOptions">Options for the regular expression.</param>
        public DataFormatGuard(string fieldName, string pattern, RegexOptions regexOptions = RegexOptions.None)
        {
            _fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            _regexOptions = regexOptions;
        }
        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            // Try to get the field value from input or state
            object? fieldValue = null;
            var contextData = new Dictionary<string, object>();
            // Check input first
            if (context.Input is IDictionary<string, object> inputDict && inputDict.TryGetValue(_fieldName, out var inputValue))
            {
                fieldValue = inputValue;
            }
            // Then check state
            else if (context.State.TryGetValue(_fieldName, out var stateValue))
            {
                fieldValue = stateValue;
            }
            contextData["FieldName"] = _fieldName;
            contextData["Pattern"] = _pattern;
            contextData["FieldValue"] = fieldValue ?? "null";
            if (fieldValue == null)
            {
                return GuardResult.Failure(
                    $"Required field '{_fieldName}' is missing",
                    context: contextData);
            }
            var stringValue = fieldValue.ToString();
            if (string.IsNullOrEmpty(stringValue))
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' is empty",
                    context: contextData);
            }
            var regex = new Regex(_pattern, _regexOptions);
            if (!regex.IsMatch(stringValue))
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' with value '{stringValue}' does not match required pattern '{_pattern}'",
                    context: contextData);
            }
            return GuardResult.Success(contextData);
        }
    }
    /// <summary>
    /// Guard that validates numeric ranges.
    /// </summary>
    public class NumericRangeGuard : IGuard
    {
        private readonly string _fieldName;
        private readonly decimal _minValue;
        private readonly decimal _maxValue;
        private readonly bool _inclusiveMin;
        private readonly bool _inclusiveMax;
        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public string GuardId => $"NumericRange_{_fieldName}_{_minValue}_{_maxValue}";
        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public string DisplayName => $"Numeric Range Validation for {_fieldName}";
        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public string Description => $"Validates that {_fieldName} is between {_minValue} and {_maxValue}";
        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public GuardSeverity Severity => GuardSeverity.Error;
        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public string Category => "Data Validation";
        /// <summary>
        /// Initializes a new instance of the NumericRangeGuard class.
        /// </summary>
        /// <param name="fieldName">The name of the numeric field to validate.</param>
        /// <param name="minValue">The minimum allowed value.</param>
        /// <param name="maxValue">The maximum allowed value.</param>
        /// <param name="inclusiveMin">Whether the minimum value is inclusive.</param>
        /// <param name="inclusiveMax">Whether the maximum value is inclusive.</param>
        public NumericRangeGuard(
            string fieldName,
            decimal minValue,
            decimal maxValue,
            bool inclusiveMin = true,
            bool inclusiveMax = true)
        {
            _fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            _minValue = minValue;
            _maxValue = maxValue;
            _inclusiveMin = inclusiveMin;
            _inclusiveMax = inclusiveMax;
        }
        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            // Try to get the field value from input or state
            object? fieldValue = null;
            var contextData = new Dictionary<string, object>
            {
                ["FieldName"] = _fieldName,
                ["MinValue"] = _minValue,
                ["MaxValue"] = _maxValue,
                ["InclusiveMin"] = _inclusiveMin,
                ["InclusiveMax"] = _inclusiveMax
            };
            // Check input first
            if (context.Input is IDictionary<string, object> inputDict && inputDict.TryGetValue(_fieldName, out var inputValue))
            {
                fieldValue = inputValue;
            }
            // Then check state
            else if (context.State.TryGetValue(_fieldName, out var stateValue))
            {
                fieldValue = stateValue;
            }
            if (fieldValue == null)
            {
                return GuardResult.Failure(
                    $"Required numeric field '{_fieldName}' is missing",
                    context: contextData);
            }
            if (!decimal.TryParse(fieldValue.ToString(), out var numericValue))
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' with value '{fieldValue}' is not a valid number",
                    context: contextData);
            }
            contextData["ActualValue"] = numericValue;
            // Check minimum bound
            if (_inclusiveMin && numericValue < _minValue)
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' value {numericValue} is less than minimum {_minValue}",
                    context: contextData);
            }
            if (!_inclusiveMin && numericValue <= _minValue)
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' value {numericValue} is less than or equal to minimum {_minValue}",
                    context: contextData);
            }
            // Check maximum bound
            if (_inclusiveMax && numericValue > _maxValue)
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' value {numericValue} is greater than maximum {_maxValue}",
                    context: contextData);
            }
            if (!_inclusiveMax && numericValue >= _maxValue)
            {
                return GuardResult.Failure(
                    $"Field '{_fieldName}' value {numericValue} is greater than or equal to maximum {_maxValue}",
                    context: contextData);
            }
            return GuardResult.Success(contextData);
        }
    }
    /// <summary>
    /// Guard that validates required fields are present and not empty.
    /// </summary>
    public class RequiredFieldGuard : IGuard
    {
        private readonly string[] _fieldNames;
        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public string GuardId => $"RequiredFields_{string.Join("_", _fieldNames)}";
        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public string DisplayName => "Required Fields Validation";
        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public string Description => $"Validates that required fields are present: {string.Join(", ", _fieldNames)}";
        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public GuardSeverity Severity => GuardSeverity.Critical;
        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public string Category => "Data Validation";
        /// <summary>
        /// Initializes a new instance of the RequiredFieldGuard class.
        /// </summary>
        /// <param name="fieldNames">The names of the required fields.</param>
        public RequiredFieldGuard(params string[] fieldNames)
        {
            _fieldNames = fieldNames ?? throw new ArgumentNullException(nameof(fieldNames));
            if (_fieldNames.Length == 0)
            {
                throw new ArgumentException("At least one field name must be specified", nameof(fieldNames));
            }
        }
        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var missingFields = new List<string>();
            var contextData = new Dictionary<string, object>
            {
                ["RequiredFields"] = _fieldNames,
                ["CheckedLocations"] = new[] { "Input", "State" }
            };
            foreach (var fieldName in _fieldNames)
            {
                // Check input
                if (context.Input is IDictionary<string, object> inputDict && inputDict.ContainsKey(fieldName))
                {
                    var value = inputDict[fieldName];
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        continue; // Field is present and not empty
                    }
                }
                // Check state
                if (context.State.ContainsKey(fieldName))
                {
                    var value = context.State[fieldName];
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        continue; // Field is present and not empty
                    }
                }
                missingFields.Add(fieldName);
            }
            if (missingFields.Any())
            {
                return GuardResult.Failure(
                    $"Required fields are missing or empty: {string.Join(", ", missingFields)}",
                    context: contextData);
            }
            return GuardResult.Success(contextData);
        }
    }
    /// <summary>
    /// Guard that validates user permissions and authorization.
    /// </summary>
    public class AuthorizationGuard : IGuard
    {
        private readonly string _requiredPermission;
        private readonly string[] _allowedRoles;
        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public string GuardId => $"Authorization_{_requiredPermission}";
        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public string DisplayName => "Authorization Validation";
        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public string Description => $"Validates that user has required permission: {_requiredPermission}";
        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public GuardSeverity Severity => GuardSeverity.Critical;
        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public string Category => "Security";
        /// <summary>
        /// Initializes a new instance of the AuthorizationGuard class.
        /// </summary>
        /// <param name="requiredPermission">The permission required for access.</param>
        /// <param name="allowedRoles">Optional roles that are allowed access.</param>
        public AuthorizationGuard(string requiredPermission, params string[] allowedRoles)
        {
            _requiredPermission = requiredPermission ?? throw new ArgumentNullException(nameof(requiredPermission));
            _allowedRoles = allowedRoles ?? Array.Empty<string>();
        }
        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var contextData = new Dictionary<string, object>
            {
                ["RequiredPermission"] = _requiredPermission,
                ["AllowedRoles"] = _allowedRoles,
                ["UserPermissions"] = new string[0], // PLACEHOLDER: Get from actual user context
                ["UserRoles"] = new string[0] // PLACEHOLDER: Get from actual user context
            };
            // PLACEHOLDER: Implement actual user context retrieval
            // This would typically come from:
            // - JWT token claims
            // - User session data
            // - External authorization service
            var userPermissions = context.GetState<string[]>("UserPermissions", Array.Empty<string>());
            var userRoles = context.GetState<string[]>("UserRoles", Array.Empty<string>());
            contextData["UserPermissions"] = userPermissions;
            contextData["UserRoles"] = userRoles;
            // Check if user has required permission
            if (!userPermissions.Contains(_requiredPermission, StringComparer.OrdinalIgnoreCase))
            {
                return GuardResult.Failure(
                    $"User does not have required permission: {_requiredPermission}",
                    failureBlockName: "access_denied",
                    context: contextData);
            }
            // If specific roles are required, check those too
            if (_allowedRoles.Length > 0)
            {
                var hasAllowedRole = userRoles.Any(role => _allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
                if (!hasAllowedRole)
                {
                    return GuardResult.Failure(
                        $"User does not have any of the required roles: {string.Join(", ", _allowedRoles)}",
                        failureBlockName: "access_denied",
                        context: contextData);
                }
            }
            return GuardResult.Success(contextData);
        }
    }
}