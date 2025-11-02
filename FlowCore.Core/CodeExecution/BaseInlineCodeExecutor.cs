namespace FlowCore.CodeExecution;
/// <summary>
/// Base implementation for inline code executors providing common functionality.
/// Reduces code duplication across inline code executor implementations.
/// </summary>
public abstract class BaseInlineCodeExecutor(CodeSecurityConfig securityConfig) : ICodeExecutor
{
    protected readonly NamespaceValidator _namespaceValidator = new NamespaceValidator(securityConfig, null);
    protected readonly TypeValidator _typeValidator = new TypeValidator(securityConfig, null);
    protected readonly CodeSecurityConfig _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));

    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    public abstract string ExecutorType { get; }
    /// <summary>
    /// Gets the list of programming languages supported by this executor.
    /// </summary>
    public virtual IReadOnlyList<string> SupportedLanguages => ["csharp", "c#"];
    /// <summary>
    /// Executes the configured code with the provided execution context.
    /// </summary>
    /// <param name="context">The execution context containing workflow state and configuration.</param>
    /// <param name="ct">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the code execution result with success status, output data, and any errors.</returns>
    public abstract Task<CodeExecutionResult> ExecuteAsync(
        CodeExecutionContext context,
        CancellationToken ct = default);
    /// <summary>
    /// Determines whether this executor can handle the specified configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor can handle the configuration, false otherwise.</returns>
    public virtual bool CanExecute(CodeExecutionConfig config) =>
        config.Mode == CodeExecutionMode.Inline &&
        SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Validates that the code can be executed safely with the given configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    public virtual ValidationResult ValidateExecutionSafety(CodeExecutionConfig config)
    {
        if (config.Mode != CodeExecutionMode.Inline)
        {
            return ValidationResult.Failure([$"This executor only supports {CodeExecutionMode.Inline} mode"]);
        }
        if (!SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure([$"Unsupported language: {config.Language}"]);
        }
        if (string.IsNullOrEmpty(config.Code))
        {
            return ValidationResult.Failure(["No code provided for execution"]);
        }
        // Use the validators to check security compliance
        var namespaceValidation = _namespaceValidator.ValidateNamespaces(config.Code);
        var typeValidation = _typeValidator.ValidateTypes(config.Code);
        if (!namespaceValidation.IsValid || !typeValidation.IsValid)
        {
            var errors = new List<string>();
            if (!namespaceValidation.IsValid)
            {
                errors.AddRange(namespaceValidation.Errors);
            }

            if (!typeValidation.IsValid)
            {
                errors.AddRange(typeValidation.Errors);
            }

            return ValidationResult.Failure(errors);
        }
        return ValidationResult.Success();
    }
    /// <summary>
    /// Validates the code for security and basic requirements.
    /// </summary>
    protected ValidationResult ValidateCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return ValidationResult.Failure(["No code provided for execution"]);
        }
        var namespaceValidation = _namespaceValidator.ValidateNamespaces(code);
        var typeValidation = _typeValidator.ValidateTypes(code);
        if (!namespaceValidation.IsValid || !typeValidation.IsValid)
        {
            var errors = new List<string>();
            if (!namespaceValidation.IsValid)
            {
                errors.AddRange(namespaceValidation.Errors);
            }

            if (!typeValidation.IsValid)
            {
                errors.AddRange(typeValidation.Errors);
            }

            return ValidationResult.Failure(errors);
        }
        return ValidationResult.Success();
    }
    /// <summary>
    /// Compiles C# code using the centralized compiler.
    /// </summary>
    protected Assembly CompileCode(string code, string className, string methodName, string returnType, string contextType) => CodeCompiler.Compile(code, className, methodName, returnType, contextType);
    /// <summary>
    /// Executes a compiled method from an assembly using the centralized compiler.
    /// </summary>
    protected object? ExecuteCompiledMethod(Assembly assembly, string className, string methodName, CodeExecutionContext context) => CodeCompiler.ExecuteMethod(assembly, className, methodName, context);
}
