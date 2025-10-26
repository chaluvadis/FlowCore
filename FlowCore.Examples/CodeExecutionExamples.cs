namespace FlowCore.Examples;
/// <summary>
/// Examples demonstrating how to use the configurable code execution system.
/// Shows various usage patterns and integration scenarios.
/// </summary>
public static class CodeExecutionExamples
{
    /// <summary>
    /// Example 1: Simple inline code execution for data transformation.
    /// </summary>
    public static async Task Example1_SimpleDataTransformation()
    {
        Console.WriteLine("=== Example 1: Simple Data Transformation ===");
        // Create security configuration
        var securityConfig = CodeSecurityConfig.CreateDefault();
        // Create code executor
        var executor = new InlineCodeExecutor(securityConfig);
        // Create execution configuration
        var codeConfig = CodeExecutionConfig.CreateInline(
            "csharp",
            @"
                var input = (string)context.GetInput();
                var transformed = input.ToUpper().Trim();
                context.SetState(""TransformedData"", transformed);
                context.SetState(""OriginalLength"", input.Length);
                context.SetState(""TransformedLength"", transformed.Length);
                return transformed;
            ",
            allowedNamespaces: new[] { "System", "System.Linq" });
        // Create execution context
        var executionContext = new Models.ExecutionContext("hello world");
        var codeContext = new CodeExecutionContext(executionContext, codeConfig, new MockServiceProvider());
        // Execute the code
        var result = await executor.ExecuteAsync(codeContext);
        Console.WriteLine($"Execution Success: {result.Success}");
        Console.WriteLine($"Result: {result.Output}");
        Console.WriteLine($"Execution Time: {result.ExecutionTime.TotalMilliseconds}ms");
        if (result.Success)
        {
            var originalLength = executionContext.GetState<int>("OriginalLength");
            var transformedLength = executionContext.GetState<int>("TransformedLength");
            var transformedData = executionContext.GetState<string>("TransformedData");
            Console.WriteLine($"Original Length: {originalLength}");
            Console.WriteLine($"Transformed Length: {transformedLength}");
            Console.WriteLine($"Transformed Data: {transformedData}");
        }
        Console.WriteLine();
    }
    /// <summary>
    /// Example 2: Assembly-based code execution for external business logic.
    /// </summary>
    public static async Task Example2_AssemblyBasedExecution()
    {
        Console.WriteLine("=== Example 2: Assembly-Based Execution ===");
        // Create security configuration
        var securityConfig = CodeSecurityConfig.CreateDefault();
        // Create assembly executor
        var executor = new AssemblyCodeExecutor(securityConfig);
        // Create execution configuration for external assembly
        var codeConfig = CodeExecutionConfig.CreateAssembly(
            "/path/to/CustomBusinessLogic.dll", // In real scenario, this would be a valid path
            "CustomBusinessLogic.OrderProcessor",
            "ProcessOrder",
            parameters: new Dictionary<string, object>
            {
                ["orderId"] = "ORD-12345",
                ["customerId"] = "CUST-67890"
            });
        // Note: This example shows the structure but would require actual assembly file
        Console.WriteLine("Assembly execution configuration created:");
        Console.WriteLine($"  Mode: {codeConfig.Mode}");
        Console.WriteLine($"  Assembly: {codeConfig.AssemblyPath}");
        Console.WriteLine($"  Type: {codeConfig.TypeName}");
        Console.WriteLine($"  Method: {codeConfig.MethodName}");
        Console.WriteLine($"  Parameters: {codeConfig.Parameters.Count}");
        Console.WriteLine();
    }
    /// <summary>
    /// Example 3: CodeBlock integration with workflow definitions.
    /// </summary>
    public static void Example3_CodeBlockWorkflowIntegration()
    {
        Console.WriteLine("=== Example 3: CodeBlock Workflow Integration ===");
        // Create workflow definition with code blocks
        var workflowDefinition = WorkflowDefinition.Create(
            id: "order-processing-workflow",
            name: "Order Processing with Custom Logic",
            startBlockName: "validate-input",
            blocks: new Dictionary<string, WorkflowBlockDefinition>
            {
                ["validate-input"] = WorkflowBlockDefinition.Create(
                    "validate-input",
                    "CodeBlock",
                    "FlowCore.CodeBlocks",
                    "enrich-data",
                    "error-handler",
                    configuration: new Dictionary<string, object>
                    {
                        ["IsCodeBlock"] = true,
                        ["Mode"] = "Inline",
                        ["Language"] = "csharp",
                        ["Code"] = @"
                            var orderAmount = (decimal)context.GetState(""OrderAmount"");
                            if (orderAmount <= 0)
                                throw new ArgumentException(""Order amount must be positive"");
                            context.SetState(""ValidationPassed"", true);
                            return true;
                        "
                    }),
                ["enrich-data"] = WorkflowBlockDefinition.Create(
                    "enrich-data",
                    "CodeBlock",
                    "FlowCore.CodeBlocks",
                    "process-order",
                    "error-handler",
                    configuration: new Dictionary<string, object>
                    {
                        ["IsCodeBlock"] = true,
                        ["Mode"] = "Inline",
                        ["Language"] = "csharp",
                        ["Code"] = @"
                            var customerId = (string)context.GetState(""CustomerId"");
                            var orderAmount = (decimal)context.GetState(""OrderAmount"");
                            // Simulate data enrichment
                            var customerTier = GetCustomerTier(customerId);
                            var discountRate = customerTier == ""Premium"" ? 0.1m : 0.05m;
                            context.SetState(""CustomerTier"", customerTier);
                            context.SetState(""DiscountRate"", discountRate);
                            context.SetState(""DiscountAmount"", orderAmount * discountRate);
                            context.SetState(""FinalAmount"", orderAmount * (1 - discountRate));
                            return true;
                        "
                    }),
                ["process-order"] = WorkflowBlockDefinition.Create(
                    "process-order",
                    "StandardBlock",
                    "FlowCore.Blocks",
                    "send-confirmation",
                    "error-handler"),
                ["error-handler"] = WorkflowBlockDefinition.Create(
                    "error-handler",
                    "CodeBlock",
                    "FlowCore.CodeBlocks",
                    "end",
                    "end",
                    configuration: new Dictionary<string, object>
                    {
                        ["IsCodeBlock"] = true,
                        ["Mode"] = "Inline",
                        ["Language"] = "csharp",
                        ["Code"] = @"
                            var errorMessage = context.GetState(""ErrorMessage"") ?? ""Unknown error occurred"";
                            LogError(new Exception(errorMessage), ""Workflow error: {0}"", errorMessage);
                            context.SetState(""ProcessingFailed"", true);
                            return false;
                        "
                    })
            });
        Console.WriteLine("Workflow definition created with code blocks:");
        Console.WriteLine($"  ID: {workflowDefinition.Id}");
        Console.WriteLine($"  Name: {workflowDefinition.Name}");
        Console.WriteLine($"  Start Block: {workflowDefinition.StartBlockName}");
        Console.WriteLine($"  Total Blocks: {workflowDefinition.Blocks.Count}");
        foreach (var block in workflowDefinition.Blocks)
        {
            Console.WriteLine($"  Block: {block.Key} -> {block.Value.BlockType}");
        }
        Console.WriteLine();
    }
    /// <summary>
    /// Example 4: CodeGuard integration for conditional execution.
    /// </summary>
    public static void Example4_CodeGuardConditionalLogic()
    {
        Console.WriteLine("=== Example 4: CodeGuard Conditional Logic ===");
        // Create security configuration
        var securityConfig = CodeSecurityConfig.CreateDefault();
        // Create guard configuration for order amount validation
        var guardConfig = CodeExecutionConfig.CreateInline(
            "csharp",
            @"
                var orderAmount = (decimal)context.GetState(""OrderAmount"");
                var customerTier = (string)context.GetState(""CustomerTier"");
                var maxAmount = customerTier == ""Premium"" ? 10000m : 5000m;
                return orderAmount > 0 && orderAmount <= maxAmount;
            ",
            allowedNamespaces: new[] { "System" });
        // Create CodeGuard instance
        var codeGuard = CodeGuard.Create(
            guardId: "order-amount-validator",
            displayName: "Order Amount Validator",
            description: "Validates that order amount is within acceptable limits based on customer tier",
            config: guardConfig,
            serviceProvider: new MockServiceProvider(),
            severity: GuardSeverity.Error,
            category: "Business Rules",
            failureBlockName: "manual-review"
        );
        Console.WriteLine("CodeGuard created:");
        Console.WriteLine($"  ID: {codeGuard.GuardId}");
        Console.WriteLine($"  Display Name: {codeGuard.DisplayName}");
        Console.WriteLine($"  Description: {codeGuard.Description}");
        Console.WriteLine($"  Severity: {codeGuard.Severity}");
        Console.WriteLine($"  Category: {codeGuard.Category}");
        Console.WriteLine($"  Failure Block: {codeGuard.FailureBlockName}");
        Console.WriteLine();
    }
    /// <summary>
    /// Example 5: Advanced security configuration for enterprise scenarios.
    /// </summary>
    public static void Example5_AdvancedSecurityConfiguration()
    {
        Console.WriteLine("=== Example 5: Advanced Security Configuration ===");
        // High-security configuration for financial processing
        var highSecurityConfig = CodeSecurityConfig.Create(
            allowedNamespaces: new[]
            {
                "System",
                "System.Linq",
                "System.Text",
                "System.Collections.Generic",
                "System.Threading.Tasks"
            },
            blockedNamespaces: new[]
            {
                "System.IO",
                "System.Net",
                "System.Reflection",
                "System.Runtime",
                "System.Diagnostics",
                "System.Management"
            },
            allowedTypes: new[]
            {
                "System.String",
                "System.Int32",
                "System.Decimal",
                "System.Boolean",
                "System.DateTime",
                "System.Collections.Generic.List`1",
                "System.Collections.Generic.Dictionary`2"
            },
            blockedTypes: new[]
            {
                "System.Reflection.*",
                "System.IO.*",
                "System.Net.*",
                "System.Diagnostics.*"
            },
            allowReflection: false,
            allowFileSystemAccess: false,
            allowNetworkAccess: false,
            allowThreading: true,
            maxMemoryUsage: 50, // 50 MB limit
            enableAuditLogging: true
        );
        Console.WriteLine("High-security configuration created:");
        Console.WriteLine($"  Allowed Namespaces: {highSecurityConfig.AllowedNamespaces.Count}");
        Console.WriteLine($"  Blocked Namespaces: {highSecurityConfig.BlockedNamespaces.Count}");
        Console.WriteLine($"  Allowed Types: {highSecurityConfig.AllowedTypes.Count}");
        Console.WriteLine($"  Blocked Types: {highSecurityConfig.BlockedTypes.Count}");
        Console.WriteLine($"  Allow Reflection: {highSecurityConfig.AllowReflection}");
        Console.WriteLine($"  Allow File System: {highSecurityConfig.AllowFileSystemAccess}");
        Console.WriteLine($"  Allow Network: {highSecurityConfig.AllowNetworkAccess}");
        Console.WriteLine($"  Max Memory: {highSecurityConfig.MaxMemoryUsage} MB");
        Console.WriteLine($"  Audit Logging: {highSecurityConfig.EnableAuditLogging}");
        Console.WriteLine();
    }
    /// <summary>
    /// Example 6: Error handling and recovery strategies.
    /// </summary>
    public static void Example6_ErrorHandlingAndRecovery()
    {
        Console.WriteLine("=== Example 6: Error Handling and Recovery ===");
        // Create error handler
        var errorHandler = new CodeExecutionErrorHandler();
        // Simulate different types of errors
        var errorScenarios = new[]
        {
            ("Security Violation", new System.Security.SecurityException("Access to blocked namespace System.IO")),
            ("Timeout Error", new TimeoutException("Operation timed out after 30 seconds")),
            ("Argument Error", new ArgumentException("Required parameter 'customerId' is missing")),
            ("Format Error", new FormatException("Invalid data format in input")),
            ("General Error", new Exception("Unexpected error occurred"))
        };
        foreach (var (errorName, error) in errorScenarios)
        {
            var context = new Models.ExecutionContext($"test input for {errorName}");
            var analysis = errorHandler.AnalyzeError(error, context, "test-block");
            Console.WriteLine($"Error: {errorName}");
            Console.WriteLine($"  Type: {analysis.ErrorType}");
            Console.WriteLine($"  Severity: {analysis.Severity}");
            Console.WriteLine($"  Recoverable: {analysis.IsRecoverable}");
            Console.WriteLine($"  Description: {analysis.Description}");
            Console.WriteLine($"  Suggested Actions: {string.Join(", ", analysis.SuggestedActions.Take(2))}");
            Console.WriteLine();
        }
    }
    /// <summary>
    /// Example 7: Performance monitoring and optimization.
    /// </summary>
    public static void Example7_PerformanceMonitoring()
    {
        Console.WriteLine("=== Example 7: Performance Monitoring ===");
        // Create performance monitor
        var performanceMonitor = new CodeExecutionPerformanceMonitor();
        // Simulate performance tracking
        var executionId = Guid.NewGuid();
        Console.WriteLine("Performance monitoring setup:");
        Console.WriteLine($"  Monitor Type: {performanceMonitor.GetType().Name}");
        Console.WriteLine($"  Tracking Enabled: true");
        Console.WriteLine($"  Metrics Collection: Execution time, memory usage, success rate");
        // Example of how performance tracking would be used
        Console.WriteLine("\nExample performance tracking workflow:");
        Console.WriteLine("1. StartExecutionTracking() - Begin timing code execution");
        Console.WriteLine("2. ExecuteCodeAsync() - Run the actual code");
        Console.WriteLine("3. CompleteExecutionTracking() - Record metrics and performance data");
        Console.WriteLine("4. GetBlockPerformanceReport() - Retrieve performance statistics");
        Console.WriteLine("5. AnalyzePerformanceBottlenecks() - Identify optimization opportunities");
        Console.WriteLine();
    }
    /// <summary>
    /// Example 8: Complete workflow with multiple code blocks and guards.
    /// </summary>
    public static void Example8_CompleteWorkflowExample()
    {
        Console.WriteLine("=== Example 8: Complete Workflow Example ===");
        // This example shows how all components work together in a real workflow
        Console.WriteLine("Complete order processing workflow with code execution:");
        Console.WriteLine();
        Console.WriteLine("Workflow: Order Processing Pipeline");
        Console.WriteLine("├── 1. Input Validation (CodeBlock)");
        Console.WriteLine("│   ├── Validates order data and format");
        Console.WriteLine("│   └── Sets validation flags in workflow state");
        Console.WriteLine("├── 2. Customer Enrichment (CodeBlock)");
        Console.WriteLine("│   ├── Looks up customer information");
        Console.WriteLine("│   ├── Calculates discounts based on tier");
        Console.WriteLine("│   └── Updates order with enriched data");
        Console.WriteLine("├── 3. Business Rules Check (CodeGuard)");
        Console.WriteLine("│   ├── Validates order against business rules");
        Console.WriteLine("│   ├── Checks amount limits and customer status");
        Console.WriteLine("│   └── Routes to manual review if needed");
        Console.WriteLine("├── 4. Order Processing (CodeBlock)");
        Console.WriteLine("│   ├── Processes payment and inventory");
        Console.WriteLine("│   ├── Updates order status");
        Console.WriteLine("│   └── Triggers fulfillment systems");
        Console.WriteLine("└── 5. Notification (CodeBlock)");
        Console.WriteLine("    ├── Sends confirmation emails");
        Console.WriteLine("    ├── Updates customer communication");
        Console.WriteLine("    └── Logs order completion");
        Console.WriteLine();
        Console.WriteLine("Key Integration Points:");
        Console.WriteLine("• CodeBlocks execute custom business logic");
        Console.WriteLine("• CodeGuards control workflow flow based on conditions");
        Console.WriteLine("• Security validation ensures safe code execution");
        Console.WriteLine("• Error handling provides robust failure recovery");
        Console.WriteLine("• Performance monitoring tracks system health");
        Console.WriteLine("• State management enables data flow between blocks");
        Console.WriteLine();
    }
    /// <summary>
    /// Mock service provider for examples.
    /// </summary>
    private class MockServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            // Return mock implementations for common services
            if (serviceType == typeof(ILogger))
                return null;
            if (serviceType == typeof(ILogger<>))
                return NullLogger.Instance;
            return null;
        }
    }
    /// <summary>
    /// Mock customer tier lookup for examples.
    /// </summary>
    private static string GetCustomerTier(string customerId) =>
        // Simulate customer tier lookup logic
        customerId.StartsWith("PREM") ? "Premium" : "Standard";
}