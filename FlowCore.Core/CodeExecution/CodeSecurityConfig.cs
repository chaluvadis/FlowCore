namespace FlowCore.CodeExecution;

/// <summary>
/// Security configuration for code execution.
/// Defines namespace and type restrictions to ensure safe code execution.
/// </summary>
public class CodeSecurityConfig
{
    /// <summary>
    /// Gets the list of allowed namespaces for code execution.
    /// Only types from these namespaces can be used in the code.
    /// </summary>
    public IReadOnlyList<string> AllowedNamespaces { get; }

    /// <summary>
    /// Gets the list of allowed types for code execution.
    /// Only these specific types can be used, even if their namespace is allowed.
    /// </summary>
    public IReadOnlyList<string> AllowedTypes { get; }

    /// <summary>
    /// Gets the list of blocked namespaces for code execution.
    /// Types from these namespaces cannot be used, even if otherwise allowed.
    /// </summary>
    public IReadOnlyList<string> BlockedNamespaces { get; }

    /// <summary>
    /// Gets the list of blocked types for code execution.
    /// These specific types cannot be used, even if their namespace is allowed.
    /// </summary>
    public IReadOnlyList<string> BlockedTypes { get; }

    /// <summary>
    /// Gets a value indicating whether to allow reflection usage.
    /// </summary>
    public bool AllowReflection { get; }

    /// <summary>
    /// Gets a value indicating whether to allow file system access.
    /// </summary>
    public bool AllowFileSystemAccess { get; }

    /// <summary>
    /// Gets a value indicating whether to allow network access.
    /// </summary>
    public bool AllowNetworkAccess { get; }

    /// <summary>
    /// Gets a value indicating whether to allow threading operations.
    /// </summary>
    public bool AllowThreading { get; }

    /// <summary>
    /// Gets the maximum memory usage allowed for code execution (in MB).
    /// </summary>
    public long MaxMemoryUsage { get; }

    /// <summary>
    /// Gets the maximum execution time allowed for code execution (in milliseconds).
    /// </summary>
    public int MaxExecutionTime { get; }

    /// <summary>
    /// Gets the maximum number of assemblies to cache.
    /// </summary>
    public int MaxAssemblyCacheSize { get; }

    /// <summary>
    /// Gets a value indicating whether to enable security audit logging.
    /// </summary>
    public bool EnableAuditLogging { get; }

    /// <summary>
    /// Gets a value indicating whether to require signed assemblies for strict security mode.
    /// </summary>
    public bool RequireSignedAssemblies { get; }

    /// <summary>
    /// Gets the list of allowed directories for assembly loading.
    /// Only assemblies from these directories can be loaded.
    /// </summary>
    public IReadOnlyList<string> AllowedDirectories { get; }

    /// <summary>
    /// Gets the list of common method suffixes for method resolution.
    /// Used when exact method name is not found.
    /// </summary>
    public IReadOnlyList<string> MethodSuffixes { get; }

    private CodeSecurityConfig(
        IReadOnlyList<string> allowedNamespaces,
        IReadOnlyList<string> allowedTypes,
        IReadOnlyList<string> blockedNamespaces,
        IReadOnlyList<string> blockedTypes,
        bool allowReflection,
        bool allowFileSystemAccess,
        bool allowNetworkAccess,
        bool allowThreading,
        long maxMemoryUsage,
        bool enableAuditLogging,
        bool requireSignedAssemblies,
        int maxExecutionTime,
        int maxAssemblyCacheSize,
        IReadOnlyList<string> allowedDirectories,
        IReadOnlyList<string> methodSuffixes)
    {
        AllowedNamespaces = allowedNamespaces ?? throw new ArgumentNullException(nameof(allowedNamespaces));
        AllowedTypes = allowedTypes ?? throw new ArgumentNullException(nameof(allowedTypes));
        BlockedNamespaces = blockedNamespaces ?? throw new ArgumentNullException(nameof(blockedNamespaces));
        BlockedTypes = blockedTypes ?? throw new ArgumentNullException(nameof(blockedTypes));
        AllowReflection = allowReflection;
        AllowFileSystemAccess = allowFileSystemAccess;
        AllowNetworkAccess = allowNetworkAccess;
        AllowThreading = allowThreading;
        MaxMemoryUsage = maxMemoryUsage;
        EnableAuditLogging = enableAuditLogging;
        RequireSignedAssemblies = requireSignedAssemblies;
        MaxExecutionTime = maxExecutionTime;
        MaxAssemblyCacheSize = maxAssemblyCacheSize;
        AllowedDirectories = allowedDirectories ?? throw new ArgumentNullException(nameof(allowedDirectories));
        MethodSuffixes = methodSuffixes ?? throw new ArgumentNullException(nameof(methodSuffixes));
    }

    /// <summary>
    /// Creates a default security configuration with restrictive settings.
    /// </summary>
    /// <returns>A new security configuration with default restrictive settings.</returns>
    public static CodeSecurityConfig CreateDefault() => new CodeSecurityConfig(
            GetDefaultAllowedNamespaces(),
            GetDefaultAllowedTypes(),
            GetDefaultBlockedNamespaces(),
            GetDefaultBlockedTypes(),
            allowReflection: false,
            allowFileSystemAccess: false,
            allowNetworkAccess: false,
            allowThreading: true,
            maxMemoryUsage: 100, // 100 MB
            enableAuditLogging: true,
            requireSignedAssemblies: true,
            maxExecutionTime: 30000, // 30 seconds
            maxAssemblyCacheSize: 10,
            allowedDirectories: [],
            methodSuffixes: GetDefaultMethodSuffixes());

    /// <summary>
    /// Creates a permissive security configuration for trusted code.
    /// </summary>
    /// <returns>A new security configuration with permissive settings.</returns>
    public static CodeSecurityConfig CreatePermissive() => new CodeSecurityConfig(
            GetPermissiveAllowedNamespaces(),
            GetPermissiveAllowedTypes(),
            [],
            [],
            allowReflection: true,
            allowFileSystemAccess: true,
            allowNetworkAccess: true,
            allowThreading: true,
            maxMemoryUsage: 500, // 500 MB
            enableAuditLogging: true,
            requireSignedAssemblies: false,
            maxExecutionTime: 60000, // 60 seconds
            maxAssemblyCacheSize: 20,
            allowedDirectories: [],
            methodSuffixes: GetDefaultMethodSuffixes());

    /// <summary>
    /// Creates a custom security configuration.
    /// </summary>
    /// <param name="allowedNamespaces">Namespaces to allow.</param>
    /// <param name="allowedTypes">Types to allow.</param>
    /// <param name="blockedNamespaces">Namespaces to block.</param>
    /// <param name="blockedTypes">Types to block.</param>
    /// <param name="allowReflection">Whether to allow reflection.</param>
    /// <param name="allowFileSystemAccess">Whether to allow file system access.</param>
    /// <param name="allowNetworkAccess">Whether to allow network access.</param>
    /// <param name="allowThreading">Whether to allow threading.</param>
    /// <param name="maxMemoryUsage">Maximum memory usage in MB.</param>
    /// <param name="enableAuditLogging">Whether to enable audit logging.</param>
    /// <param name="requireSignedAssemblies">Whether to require signed assemblies for strict security.</param>
    /// <param name="allowedDirectories">Directories allowed for assembly loading.</param>
    /// <returns>A new custom security configuration.</returns>
    public static CodeSecurityConfig Create(
        IReadOnlyList<string>? allowedNamespaces = null,
        IReadOnlyList<string>? allowedTypes = null,
        IReadOnlyList<string>? blockedNamespaces = null,
        IReadOnlyList<string>? blockedTypes = null,
        bool allowReflection = false,
        bool allowFileSystemAccess = false,
        bool allowNetworkAccess = false,
        bool allowThreading = true,
        long maxMemoryUsage = 100,
        bool enableAuditLogging = true,
        bool requireSignedAssemblies = false,
        int maxExecutionTime = 30000,
        int maxAssemblyCacheSize = 10,
        IReadOnlyList<string>? allowedDirectories = null,
        IReadOnlyList<string>? methodSuffixes = null) => new CodeSecurityConfig(
            allowedNamespaces ?? GetDefaultAllowedNamespaces(),
            allowedTypes ?? GetDefaultAllowedTypes(),
            blockedNamespaces ?? GetDefaultBlockedNamespaces(),
            blockedTypes ?? GetDefaultBlockedTypes(),
            allowReflection,
            allowFileSystemAccess,
            allowNetworkAccess,
            allowThreading,
            maxMemoryUsage,
            enableAuditLogging,
            requireSignedAssemblies,
            maxExecutionTime,
            maxAssemblyCacheSize,
            allowedDirectories ?? [],
            methodSuffixes ?? GetDefaultMethodSuffixes());

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
            "System.Runtime.CompilerServices",
            "System.Diagnostics"
        ];

    private static IReadOnlyList<string> GetDefaultBlockedTypes() => [
            "System.Reflection.Assembly",
            "System.IO.File",
            "System.IO.Directory",
            "System.Net.WebClient",
            "System.Diagnostics.Process"
        ];

    private static IReadOnlyList<string> GetPermissiveAllowedNamespaces() => [
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading",
            "System.Threading.Tasks",
            "System.IO",
            "System.Net",
            "System.Reflection",
            "System.Runtime",
            "System.Diagnostics"
        ];

    private static IReadOnlyList<string> GetPermissiveAllowedTypes() => [
        "System.String",
        "System.Int32",
        "System.Boolean",
        "System.DateTime",
        "System.Decimal",
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.IEnumerable`1",
        "System.Linq.Enumerable",
        "System.IO.File",
        "System.IO.Directory",
        "System.Net.WebClient",
        "System.Reflection.Assembly"
    ];

    private static IReadOnlyList<string> GetDefaultMethodSuffixes() => [
        "Execute",
        "Run",
        "Process",
        "Handle"
    ];
}