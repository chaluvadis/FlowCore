using System.Text.RegularExpressions;

namespace FlowCore.CodeExecution.Security;

/// <summary>
/// Advanced validator for namespace usage in code execution.
/// Provides sophisticated pattern matching and hierarchical validation.
/// </summary>
public class NamespaceValidator
{
    private readonly CodeSecurityConfig _securityConfig;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the NamespaceValidator.
    /// </summary>
    /// <param name="securityConfig">The security configuration to validate against.</param>
    /// <param name="logger">Optional logger for validation operations.</param>
    public NamespaceValidator(CodeSecurityConfig securityConfig, ILogger? logger = null)
    {
        _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
        _logger = logger;
    }

    /// <summary>
    /// Validates namespace usage in the provided code.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <returns>A validation result indicating whether namespace usage is allowed.</returns>
    public ValidationResult ValidateNamespaces(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return ValidationResult.Success();
        }

        var violations = new List<string>();

        try
        {
            // Extract all namespace references from the code
            var namespaceReferences = ExtractNamespaceReferences(code);

            foreach (var namespaceReference in namespaceReferences)
            {
                var namespaceValidation = ValidateNamespaceReference(namespaceReference);
                if (!namespaceValidation.IsValid)
                {
                    violations.AddRange(namespaceValidation.Errors);
                }
            }

            // Check for wildcard namespace usage
            var wildcardValidation = ValidateWildcardNamespaces(code);
            if (!wildcardValidation.IsValid)
            {
                violations.AddRange(wildcardValidation.Errors);
            }

            // Check for dynamic namespace construction
            var dynamicValidation = ValidateDynamicNamespaces(code);
            if (!dynamicValidation.IsValid)
            {
                violations.AddRange(dynamicValidation.Errors);
            }

            if (violations.Any())
            {
                _logger?.LogWarning("Namespace validation failed: {Violations}", string.Join(", ", violations));
                return ValidationResult.Failure(violations);
            }

            _logger?.LogDebug("Namespace validation passed for code");
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during namespace validation");
            return ValidationResult.Failure($"Namespace validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a specific namespace reference.
    /// </summary>
    /// <param name="namespaceReference">The namespace reference to validate.</param>
    /// <returns>A validation result for the specific namespace.</returns>
    public ValidationResult ValidateNamespaceReference(string namespaceReference)
    {
        if (string.IsNullOrEmpty(namespaceReference))
        {
            return ValidationResult.Success();
        }

        var violations = new List<string>();

        // Check against blocked namespaces first (highest priority)
        foreach (var blockedNamespace in _securityConfig.BlockedNamespaces)
        {
            if (IsNamespaceMatch(namespaceReference, blockedNamespace))
            {
                violations.Add($"Blocked namespace usage: {namespaceReference} matches {blockedNamespace}");
            }
        }

        // If we have restrictive allowlist, check against it
        if (_securityConfig.AllowedNamespaces.Any())
        {
            var isAllowed = _securityConfig.AllowedNamespaces.Any(allowed =>
                IsNamespaceMatch(namespaceReference, allowed));

            if (!isAllowed)
            {
                violations.Add($"Disallowed namespace usage: {namespaceReference}");
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations) : ValidationResult.Success();
    }

    private List<string> ExtractNamespaceReferences(string code)
    {
        var namespaces = new List<string>();

        // Extract using directives
        var usingMatches = Regex.Matches(code, @"using\s+([^;]+);", RegexOptions.IgnoreCase);
        foreach (Match match in usingMatches)
        {
            var namespaceName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                namespaces.Add(namespaceName);
            }
        }

        // Extract namespace declarations
        var namespaceMatches = Regex.Matches(code, @"namespace\s+([^{\s]+)", RegexOptions.IgnoreCase);
        foreach (Match match in namespaceMatches)
        {
            var namespaceName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                namespaces.Add(namespaceName);
            }
        }

        // Extract type references that include namespaces
        var typeMatches = Regex.Matches(code, @"(?:new\s+)?([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)+)");
        foreach (Match match in typeMatches)
        {
            var typeReference = match.Groups[1].Value;
            if (typeReference.Contains('.'))
            {
                var namespacePart = typeReference.Substring(0, typeReference.LastIndexOf('.'));
                if (!string.IsNullOrEmpty(namespacePart))
                {
                    namespaces.Add(namespacePart);
                }
            }
        }

        // Remove duplicates and system namespaces that are always allowed
        return namespaces
            .Distinct()
            .Where(ns => !IsSystemNamespace(ns))
            .ToList();
    }

    private ValidationResult ValidateWildcardNamespaces(string code)
    {
        var violations = new List<string>();

        // Check for wildcard using directives
        var wildcardPatterns = new[]
        {
            @"using\s+[^;]*\*", // using System.*;
            @"using\s+static\s+[^;]*\*", // using static System.*;
        };

        foreach (var pattern in wildcardPatterns)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
            {
                violations.Add($"Wildcard namespace usage detected: {pattern}");
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations) : ValidationResult.Success();
    }

    private ValidationResult ValidateDynamicNamespaces(string code)
    {
        var violations = new List<string>();

        // Check for dynamic namespace construction patterns
        var dynamicPatterns = new[]
        {
            @"string\s*\w*\s*=\s*""[^""]*""\s*\+\s*""[^""]*""", // String concatenation for namespaces
            @"GetType\(\)\.Namespace", // Runtime namespace reflection
            @"typeof\([^)]+\)\.Namespace", // Type namespace reflection
            @"Assembly\.Get", // Assembly reflection that could lead to namespace access
        };

        foreach (var pattern in dynamicPatterns)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
            {
                violations.Add($"Dynamic namespace construction detected: {pattern}");
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations) : ValidationResult.Success();
    }

    private bool IsNamespaceMatch(string namespaceReference, string namespacePattern)
    {
        // Exact match
        if (namespaceReference.Equals(namespacePattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Hierarchical match (namespacePattern is a parent namespace)
        if (namespaceReference.StartsWith(namespacePattern + ".", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Pattern match with wildcards
        var pattern = "^" + Regex.Escape(namespacePattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(namespaceReference, pattern, RegexOptions.IgnoreCase);
    }

    private bool IsSystemNamespace(string namespaceName)
    {
        // Always allow core system namespaces
        var systemNamespaces = new[]
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading",
            "System.Threading.Tasks"
        };

        return systemNamespaces.Contains(namespaceName) ||
               systemNamespaces.Any(sys => namespaceName.StartsWith(sys + "."));
    }
}