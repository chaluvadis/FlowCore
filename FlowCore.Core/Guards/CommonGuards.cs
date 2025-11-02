namespace FlowCore.Guards;

/// <summary>
/// Common guard implementations for typical validation scenarios.
/// </summary>
public static class CommonGuards
{
    /// <summary>
    /// Guard that validates business hours.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the BusinessHoursGuard class.
    /// </remarks>
    /// <param name="startTime">The start of business hours.</param>
    /// <param name="endTime">The end of business hours.</param>
    /// <param name="validDays">The valid days of the week (defaults to weekdays).</param>
    /// <param name="holidayDates">Optional holiday dates to exclude.</param>
    public class BusinessHoursGuard(
        TimeSpan startTime,
        TimeSpan endTime,
        DayOfWeek[]? validDays = null,
        string[]? holidayDates = null) : BaseGuard
    {
        private readonly DayOfWeek[] _validDays = validDays ?? [
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        ];
        private readonly string[] _holidayDates = holidayDates ?? [];

        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public override string GuardId => $"BusinessHours_{startTime:hhmm}_{endTime:hhmm}";

        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public override string DisplayName => "Business Hours Validation";

        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public override string Description => $"Validates that current time is within business hours {startTime:hh:mm} to {endTime:hh:mm}";

        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public override string Category => "Business Rules";

        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public override async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var now = DateTime.UtcNow;
            var contextData = CreateContextData(
                ("CurrentTime", now),
                ("StartTime", startTime),
                ("EndTime", endTime),
                ("IsWeekend", !_validDays.Contains(now.DayOfWeek)),
                ("ValidDays", _validDays)
            );

            // Check if today is a valid day
            if (!_validDays.Contains(now.DayOfWeek))
            {
                return GuardResult.Failure(
                    $"Operation attempted outside business days. Current day: {now.DayOfWeek}",
                    severity: GuardSeverity.Warning,
                    context: contextData);
            }

            // Check if today is a holiday
            var todayString = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (_holidayDates.Contains(todayString))
            {
                return GuardResult.Failure(
                    $"Operation attempted on holiday: {todayString}",
                    severity: GuardSeverity.Warning,
                    context: contextData);
            }

            // Check business hours
            var currentTime = now.TimeOfDay;
            if (currentTime < startTime || currentTime > endTime)
            {
                return GuardResult.Failure(
                    $"Operation attempted outside business hours. Current time: {currentTime:hh\\:mm}, Business hours: {startTime:hh\\:mm} - {endTime:hh\\:mm}",
                    context: contextData);
            }

            return GuardResult.Success(contextData);
        }
    }
    /// <summary>
    /// Guard that validates data format using regular expressions.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the DataFormatGuard class.
    /// </remarks>
    /// <param name="fieldName">The name of the field to validate.</param>
    /// <param name="pattern">The regular expression pattern to match against.</param>
    /// <param name="regexOptions">Options for the regular expression.</param>
    public class DataFormatGuard(string fieldName, string pattern, RegexOptions regexOptions = RegexOptions.None) : BaseGuard
    {
        private readonly string _fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        private readonly string _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));

        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public override string GuardId => $"DataFormat_{_fieldName}";

        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public override string DisplayName => $"Data Format Validation for {_fieldName}";

        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public override string Description => $"Validates that {_fieldName} matches pattern {_pattern}";

        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public override async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var fieldValue = GetFieldValue(context, _fieldName);
            var contextData = CreateContextData(
                ("FieldName", _fieldName),
                ("Pattern", _pattern),
                ("FieldValue", fieldValue ?? "null")
            );

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

            var regex = new Regex(_pattern, regexOptions);
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
    /// <remarks>
    /// Initializes a new instance of the NumericRangeGuard class.
    /// </remarks>
    /// <param name="fieldName">The name of the numeric field to validate.</param>
    /// <param name="minValue">The minimum allowed value.</param>
    /// <param name="maxValue">The maximum allowed value.</param>
    /// <param name="inclusiveMin">Whether the minimum value is inclusive.</param>
    /// <param name="inclusiveMax">Whether the maximum value is inclusive.</param>
    public class NumericRangeGuard(
        string fieldName,
        decimal minValue,
        decimal maxValue,
        bool inclusiveMin = true,
        bool inclusiveMax = true) : BaseGuard
    {
        private readonly string _fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));

        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public override string GuardId => $"NumericRange_{_fieldName}_{minValue}_{maxValue}";

        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public override string DisplayName => $"Numeric Range Validation for {_fieldName}";

        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public override string Description => $"Validates that {_fieldName} is between {minValue} and {maxValue}";

        /// <summary>
        /// Helper method to check if a value is within the specified range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="inclusiveMin">Whether min is inclusive.</param>
        /// <param name="inclusiveMax">Whether max is inclusive.</param>
        /// <returns>True if within range, false otherwise.</returns>
        private static bool IsWithinRange(decimal value, decimal min, decimal max, bool inclusiveMin, bool inclusiveMax)
        {
            var minCheck = inclusiveMin ? value >= min : value > min;
            var maxCheck = inclusiveMax ? value <= max : value < max;
            return minCheck && maxCheck;
        }

        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public override async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var fieldValue = GetFieldValue(context, _fieldName);
            var contextData = CreateContextData(
                ("FieldName", _fieldName),
                ("MinValue", minValue),
                ("MaxValue", maxValue),
                ("InclusiveMin", inclusiveMin),
                ("InclusiveMax", inclusiveMax)
            );

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
            if (!IsWithinRange(numericValue, minValue, maxValue, inclusiveMin, inclusiveMax))
            {
                var comparison = inclusiveMin ? "less than" : "less than or equal to";
                if (numericValue < minValue || (inclusiveMin && numericValue == minValue))
                {
                    return GuardResult.Failure(
                        $"Field '{_fieldName}' value {numericValue} is {comparison} minimum {minValue}",
                        context: contextData);
                }
                return GuardResult.Failure(
                    $"Field '{_fieldName}' value {numericValue} is greater than maximum {maxValue}",
                    context: contextData);
            }

            return GuardResult.Success(contextData);
        }
    }
    /// <summary>
    /// Guard that validates required fields are present and not empty.
    /// </summary>
    public class RequiredFieldGuard : BaseGuard
    {
        private readonly string[] _fieldNames;

        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public override string GuardId => $"RequiredFields_{string.Join("_", _fieldNames)}";

        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public override string DisplayName => "Required Fields Validation";

        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public override string Description => $"Validates that required fields are present: {string.Join(", ", _fieldNames)}";

        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public override GuardSeverity Severity => GuardSeverity.Critical;

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
        public override async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            var missingFields = new List<string>();
            var contextData = CreateContextData(
                ("RequiredFields", _fieldNames),
                ("CheckedLocations", new[] { "Input", "State" })
            );

            foreach (var fieldName in _fieldNames)
            {
                var value = GetFieldValue(context, fieldName);
                if (value == null || string.IsNullOrEmpty(value.ToString()))
                {
                    missingFields.Add(fieldName);
                }
            }

            if (missingFields.Count != 0)
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
    /// <remarks>
    /// Initializes a new instance of the AuthorizationGuard class.
    /// </remarks>
    /// <param name="requiredPermission">The permission required for access.</param>
    /// <param name="allowedRoles">Optional roles that are allowed access.</param>
    public class AuthorizationGuard(string requiredPermission, params string[] allowedRoles) : BaseGuard
    {
        private readonly string _requiredPermission = requiredPermission ?? throw new ArgumentNullException(nameof(requiredPermission));
        private readonly string[] _allowedRoles = allowedRoles ?? [];

        /// <summary>
        /// Gets the unique identifier for this guard.
        /// </summary>
        public override string GuardId => $"Authorization_{_requiredPermission}";

        /// <summary>
        /// Gets the display name for this guard.
        /// </summary>
        public override string DisplayName => "Authorization Validation";

        /// <summary>
        /// Gets the description of what this guard validates.
        /// </summary>
        public override string Description => $"Validates that user has required permission: {_requiredPermission}";

        /// <summary>
        /// Gets the severity level of this guard.
        /// </summary>
        public override GuardSeverity Severity => GuardSeverity.Critical;

        /// <summary>
        /// Gets the category of this guard.
        /// </summary>
        public override string Category => "Security";

        /// <summary>
        /// Evaluates the guard condition against the provided context.
        /// </summary>
        /// <param name="context">The execution context to evaluate against.</param>
        /// <returns>A guard result indicating whether the condition passed or failed.</returns>
        public override async Task<GuardResult> EvaluateAsync(ExecutionContext context)
        {
            // PLACEHOLDER: Implement actual user context retrieval
            // This would typically come from:
            // - JWT token claims
            // - User session data
            // - External authorization service
            var userPermissions = context.GetState<string[]>("UserPermissions", []);
            var userRoles = context.GetState<string[]>("UserRoles", []);

            var contextData = CreateContextData(
                ("RequiredPermission", _requiredPermission),
                ("AllowedRoles", _allowedRoles),
                ("UserPermissions", userPermissions),
                ("UserRoles", userRoles)
            );

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
