using System.Text.RegularExpressions;

namespace FlowCore.CodeExecution.Security;

/// <summary>
/// Advanced validator for type usage in code execution.
/// Provides sophisticated type reference validation and security checking.
/// </summary>
public class TypeValidator
{
    private readonly CodeSecurityConfig _securityConfig;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the TypeValidator.
    /// </summary>
    /// <param name="securityConfig">The security configuration to validate against.</param>
    /// <param name="logger">Optional logger for validation operations.</param>
    public TypeValidator(CodeSecurityConfig securityConfig, ILogger? logger = null)
    {
        _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
        _logger = logger;
    }

    /// <summary>
    /// Validates type usage in the provided code.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <returns>A validation result indicating whether type usage is allowed.</returns>
    public ValidationResult ValidateTypes(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return ValidationResult.Success();
        }

        var violations = new List<string>();

        try
        {
            // Extract all type references from the code
            var typeReferences = ExtractTypeReferences(code);

            foreach (var typeReference in typeReferences)
            {
                var typeValidation = ValidateTypeReference(typeReference);
                if (!typeValidation.IsValid)
                {
                    violations.AddRange(typeValidation.Errors);
                }
            }

            // Check for generic type usage
            var genericValidation = ValidateGenericTypes(code);
            if (!genericValidation.IsValid)
            {
                violations.AddRange(genericValidation.Errors);
            }

            // Check for dynamic type usage
            var dynamicValidation = ValidateDynamicTypes(code);
            if (!dynamicValidation.IsValid)
            {
                violations.AddRange(dynamicValidation.Errors);
            }

            if (violations.Any())
            {
                _logger?.LogWarning("Type validation failed: {Violations}", string.Join(", ", violations));
                return ValidationResult.Failure(violations);
            }

            _logger?.LogDebug("Type validation passed for code");
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during type validation");
            return ValidationResult.Failure(new[] { $"Type validation error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Validates a specific type reference.
    /// </summary>
    /// <param name="typeReference">The type reference to validate.</param>
    /// <returns>A validation result for the specific type.</returns>
    public ValidationResult ValidateTypeReference(string typeReference)
    {
        if (string.IsNullOrEmpty(typeReference))
        {
            return ValidationResult.Success();
        }

        var violations = new List<string>();

        // Skip primitive types
        if (IsPrimitiveType(typeReference))
        {
            return ValidationResult.Success();
        }

        // Check against blocked types first (highest priority)
        foreach (var blockedType in _securityConfig.BlockedTypes)
        {
            if (IsTypeMatch(typeReference, blockedType))
            {
                violations.Add($"Blocked type usage: {typeReference} matches {blockedType}");
            }
        }

        // If we have restrictive allowlist, check against it
        if (_securityConfig.AllowedTypes.Any())
        {
            var isAllowed = _securityConfig.AllowedTypes.Any(allowed =>
                IsTypeMatch(typeReference, allowed));

            if (!isAllowed)
            {
                violations.Add($"Disallowed type usage: {typeReference}");
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations) : ValidationResult.Success();
    }

    private List<string> ExtractTypeReferences(string code)
    {
        var types = new List<string>();

        // Extract type references from various patterns
        var patterns = new[]
        {
            @"new\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)", // new Type()
            @"([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\s+[a-zA-Z_]", // Type variable
            @"typeof\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)", // typeof(Type)
            @"is\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)", // variable is Type
            @"as\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)", // variable as Type
            @":\s*([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)", // : Type (inheritance)
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(code, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var typeName = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(typeName) && !IsPrimitiveType(typeName))
                {
                    types.Add(typeName);
                }
            }
        }

        // Extract generic type arguments
        var genericMatches = Regex.Matches(code, @"<([^>]+)>");
        foreach (Match match in genericMatches)
        {
            var genericArgs = match.Groups[1].Value.Split(',').Select(arg => arg.Trim());
            foreach (var arg in genericArgs)
            {
                if (!string.IsNullOrEmpty(arg) && !IsPrimitiveType(arg))
                {
                    types.Add(arg);
                }
            }
        }

        return types.Distinct().ToList();
    }

    private ValidationResult ValidateGenericTypes(string code)
    {
        var violations = new List<string>();

        // Check for potentially dangerous generic type patterns
        var dangerousGenericPatterns = new[]
        {
            @"Activator\s*<[^>]*>", // Generic Activator usage
            @"Reflection\s*<[^>]*>", // Generic Reflection usage
            @"Runtime\s*<[^>]*>", // Generic Runtime usage
        };

        foreach (var pattern in dangerousGenericPatterns)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
            {
                violations.Add($"Potentially dangerous generic type usage: {pattern}");
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations) : ValidationResult.Success();
    }

    private ValidationResult ValidateDynamicTypes(string code)
    {
        var violations = new List<string>();

        // Check for dynamic type usage patterns
        var dynamicPatterns = new[]
        {
            @"\bdynamic\b", // dynamic keyword
            @"object\s*GetType\s*\(\s*\)", // Runtime type reflection
            @"Type\.GetType\s*\(", // Type.GetType usage
            @"Assembly\.GetType\s*\(", // Assembly.GetType usage
            @"GetType\s*\(\s*\)\.GetMethod", // Method reflection
            @"GetType\s*\(\s*\)\.GetProperty", // Property reflection
            @"GetType\s*\(\s*\)\.GetField", // Field reflection
        };

        foreach (var pattern in dynamicPatterns)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
            {
                violations.Add($"Dynamic type usage detected: {pattern}");
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations) : ValidationResult.Success();
    }

    private bool IsTypeMatch(string typeReference, string typePattern)
    {
        // Handle generic types
        var genericMatch = Regex.Match(typeReference, @"^(.+)<(.+)>$");
        if (genericMatch.Success)
        {
            var baseType = genericMatch.Groups[1].Value;
            var genericArgs = genericMatch.Groups[2].Value.Split(',').Select(arg => arg.Trim());

            // Check if base type matches
            if (baseType.Equals(typePattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check if any generic argument matches
            if (genericArgs.Any(arg => arg.Equals(typePattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Exact match
        if (typeReference.Equals(typePattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Pattern match with wildcards
        var pattern = "^" + Regex.Escape(typePattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(typeReference, pattern, RegexOptions.IgnoreCase);
    }

    private bool IsPrimitiveType(string typeName)
    {
        var primitiveTypes = new[]
        {
            "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
            "long", "ulong", "short", "ushort", "string", "object", "void", "var",
            "System.Boolean", "System.Byte", "System.SByte", "System.Char",
            "System.Decimal", "System.Double", "System.Single", "System.Int32",
            "System.UInt32", "System.Int64", "System.UInt64", "System.Int16",
            "System.UInt16", "System.String", "System.Object"
        };

        return primitiveTypes.Contains(typeName) ||
               primitiveTypes.Contains(typeName.Split('<').First()); // Handle generics
    }
}