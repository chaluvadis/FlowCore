namespace FlowCore.CodeExecution;
/// <summary>
/// Centralized code compilation service for dynamic C# code execution.
/// Consolidates compilation logic across different executors.
/// </summary>
public static class CodeCompiler
{
    private static readonly ConcurrentDictionary<string, (Assembly assembly, DateTime timestamp)> _compilationCache = new();
    /// <summary>
    /// Compiles C# code into an assembly with the specified parameters.
    /// </summary>
    /// <param name="code">The C# code to compile.</param>
    /// <param name="className">The name of the class to generate.</param>
    /// <param name="methodName">The name of the method to generate.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="contextType">The type of the context parameter.</param>
    /// <param name="additionalReferences">Additional metadata references to include.</param>
    /// <returns>The compiled assembly.</returns>
    public static Assembly Compile(
        string code,
        string className,
        string methodName,
        string returnType,
        string contextType,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var cacheKey = GenerateCacheKey(code, className, methodName, returnType, contextType);
        // Check cache first
        if (_compilationCache.TryGetValue(cacheKey, out var cached))
        {
            return cached.assembly;
        }
        var classCode = GenerateClassCode(code, className, methodName, returnType, contextType);
        var compilation = CSharpCompilation.Create(
            className,
            [CSharpSyntaxTree.ParseText(classCode)],
            GetDefaultReferences().Concat(additionalReferences ?? []),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine,
                result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        // Cache the compiled assembly
        CacheUtility.AddOrUpdateWithTimestamp(_compilationCache, cacheKey, assembly, 50);
        return assembly;
    }
    /// <summary>
    /// Executes a compiled method from an assembly.
    /// </summary>
    /// <param name="assembly">The compiled assembly.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method to execute.</param>
    /// <param name="context">The context parameter to pass to the method.</param>
    /// <returns>The result of the method execution.</returns>
    public static object? ExecuteMethod(Assembly assembly, string className, string methodName, object context)
    {
        var type = assembly.GetType(className);
        var method = type?.GetMethod(methodName);
        if (method == null)
        {
            throw new InvalidOperationException($"Compiled code does not contain {methodName} method");
        }
        var instance = Activator.CreateInstance(type!);
        return method.Invoke(instance, [context]);
    }
    private static IEnumerable<MetadataReference> GetDefaultReferences() => [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CodeExecutionContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
        ];
    private static string GenerateClassCode(string code, string className, string methodName, string returnType, string contextType) => $@"
using FlowCore.CodeExecution;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
public class {className}
{{
    public {returnType} {methodName}({contextType} context)
    {{
        {(returnType.Contains("async") ? "context.CancellationToken.ThrowIfCancellationRequested();" : "")}
        {code}
    }}
}}";
    private static string GenerateCacheKey(string code, string className, string methodName, string returnType, string contextType) => $"{code.GetHashCode():X8}_{className}_{methodName}_{returnType}_{contextType}";
}