namespace FlowCore.CodeExecution;

/// <summary>
/// Configuration for code execution within workflow blocks and guards.
/// Defines how code should be executed, including security settings and parameters.
/// </summary>
public class CodeExecutionConfig
{
    /// <summary>
    /// Gets the mode of code execution (inline or assembly-based).
    /// </summary>
    public CodeExecutionMode Mode { get; }

    /// <summary>
    /// Gets the programming language of the code to execute.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the source code to execute (for inline mode).
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the path to the assembly file (for assembly mode).
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the full type name to instantiate (for assembly mode).
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the method name to execute (for assembly mode).
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the list of allowed namespaces for code execution.
    /// </summary>
    public IReadOnlyList<string> AllowedNamespaces { get; }

    /// <summary>
    /// Gets the list of allowed types for code execution.
    /// </summary>
    public IReadOnlyList<string> AllowedTypes { get; }

    /// <summary>
    /// Gets the list of blocked namespaces for code execution.
    /// </summary>
    public IReadOnlyList<string> BlockedNamespaces { get; }

    /// <summary>
    /// Gets the parameters to pass to the code execution.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; }

    /// <summary>
    /// Gets the maximum time allowed for code execution.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets a value indicating whether to enable detailed logging.
    /// </summary>
    public bool EnableLogging { get; }

    /// <summary>
    /// Gets a value indicating whether to validate code before execution.
    /// </summary>
    public bool ValidateCode { get; }

    private CodeExecutionConfig(
        CodeExecutionMode mode,
        string language,
        string code,
        string assemblyPath,
        string typeName,
        string methodName,
        IReadOnlyList<string> allowedNamespaces,
        IReadOnlyList<string> allowedTypes,
        IReadOnlyList<string> blockedNamespaces,
        IReadOnlyDictionary<string, object> parameters,
        TimeSpan timeout,
        bool enableLogging,
        bool validateCode)
    {
        Mode = mode;
        Language = language ?? throw new ArgumentNullException(nameof(language));
        Code = code ?? string.Empty;
        AssemblyPath = assemblyPath ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        MethodName = methodName ?? string.Empty;
        AllowedNamespaces = allowedNamespaces ?? [];
        AllowedTypes = allowedTypes ?? [];
        BlockedNamespaces = blockedNamespaces ?? [];
        Parameters = parameters ?? new Dictionary<string, object>();
        Timeout = timeout;
        EnableLogging = enableLogging;
        ValidateCode = validateCode;
    }

    /// <summary>
    /// Creates a configuration for inline code execution.
    /// </summary>
    /// <param name="language">The programming language of the code.</param>
    /// <param name="code">The source code to execute.</param>
    /// <param name="allowedNamespaces">Namespaces allowed in the code.</param>
    /// <param name="allowedTypes">Types allowed in the code.</param>
    /// <param name="parameters">Parameters to pass to the code.</param>
    /// <param name="timeout">Maximum execution time.</param>
    /// <param name="enableLogging">Whether to enable detailed logging.</param>
    /// <param name="validateCode">Whether to validate code before execution.</param>
    /// <returns>A new inline code execution configuration.</returns>
    public static CodeExecutionConfig CreateInline(
        string language,
        string code,
        IReadOnlyList<string>? allowedNamespaces = null,
        IReadOnlyList<string>? allowedTypes = null,
        IReadOnlyList<string>? blockedNamespaces = null,
        IReadOnlyDictionary<string, object>? parameters = null,
        TimeSpan? timeout = null,
        bool enableLogging = true,
        bool validateCode = true) => new CodeExecutionConfig(
            CodeExecutionMode.Inline,
            language,
            code,
            string.Empty,
            string.Empty,
            string.Empty,
            allowedNamespaces ?? GetDefaultAllowedNamespaces(),
            allowedTypes ?? GetDefaultAllowedTypes(),
            blockedNamespaces ?? GetDefaultBlockedNamespaces(),
            parameters ?? new Dictionary<string, object>(),
            timeout ?? TimeSpan.FromSeconds(30),
            enableLogging,
            validateCode);

    /// <summary>
    /// Creates a configuration for assembly-based code execution.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <param name="typeName">Full name of the type to instantiate.</param>
    /// <param name="methodName">Name of the method to execute.</param>
    /// <param name="parameters">Parameters to pass to the method.</param>
    /// <param name="timeout">Maximum execution time.</param>
    /// <param name="enableLogging">Whether to enable detailed logging.</param>
    /// <returns>A new assembly code execution configuration.</returns>
    public static CodeExecutionConfig CreateAssembly(
        string assemblyPath,
        string typeName,
        string methodName = "Execute",
        IReadOnlyDictionary<string, object>? parameters = null,
        TimeSpan? timeout = null,
        bool enableLogging = true) => new CodeExecutionConfig(
            CodeExecutionMode.Assembly,
            "csharp",
            string.Empty,
            assemblyPath,
            typeName,
            methodName,
            [],
            [],
            [],
            parameters ?? new Dictionary<string, object>(),
            timeout ?? TimeSpan.FromSeconds(30),
            enableLogging,
            true);

    private static IReadOnlyList<string> GetDefaultAllowedNamespaces() => [
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading.Tasks"
        ];

    private static IReadOnlyList<string> GetDefaultAllowedTypes() => [
            "System.String",
            "System.Int32",
            "System.Boolean",
            "System.DateTime",
            "System.Collections.Generic.List`1",
            "System.Collections.Generic.Dictionary`2"
        ];

    private static IReadOnlyList<string> GetDefaultBlockedNamespaces() => [
            "System.IO",
            "System.Net",
            "System.Reflection",
            "System.Runtime.CompilerServices"
        ];
}

/// <summary>
/// Modes of code execution supported by the system.
/// </summary>
public enum CodeExecutionMode
{
    /// <summary>
    /// Execute code provided as a string (inline compilation).
    /// </summary>
    Inline,

    /// <summary>
    /// Execute code from a pre-compiled assembly.
    /// </summary>
    Assembly
}