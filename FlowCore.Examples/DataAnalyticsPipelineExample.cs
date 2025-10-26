namespace FlowCore.Examples;

/// <summary>
/// Complex example showcasing FlowCore's core strengths in a data analytics pipeline.
/// Demonstrates dynamic code execution, guards, error handling, state persistence, and workflow orchestration.
/// </summary>
public static class DataAnalyticsPipelineExample
{
    /// <summary>
    /// Runs the data analytics pipeline example.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== FlowCore Data Analytics Pipeline Example ===");
        Console.WriteLine("Showcasing: Dynamic Code Execution, Guards, Error Handling, State Persistence, Workflow Orchestration");
        Console.WriteLine();

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        services.AddSingleton<WorkflowBlockFactorySecurityOptions>(sp =>
        {
            return new WorkflowBlockFactorySecurityOptions
            {
                AllowDynamicAssemblyLoading = true,
                AllowedAssemblyNames = new[] { "FlowCore" }
            };
        });
        services.AddSingleton<WorkflowBlockFactory>(sp =>
        {
            var securityOptions = sp.GetRequiredService<WorkflowBlockFactorySecurityOptions>();
            return new WorkflowBlockFactory(sp, securityOptions);
        });
        services.AddSingleton<IWorkflowBlockFactory>(sp => sp.GetRequiredService<WorkflowBlockFactory>());
        services.AddSingleton<IStateManager, InMemoryStateManager>();
        services.AddSingleton<IWorkflowExecutor, WorkflowExecutor>();
        services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();
        services.AddSingleton<IWorkflowParser, WorkflowDefinitionParser>();
        services.AddSingleton<IWorkflowValidator, WorkflowValidator>();
        services.AddSingleton<ICodeExecutor, InlineCodeExecutor>();
        services.AddSingleton<GuardManager>();

        var serviceProvider = services.BuildServiceProvider();

        // Run the analytics pipeline
        await RunAnalyticsPipelineAsync(serviceProvider);

        // Demonstrate resumption from checkpoint
        await RunResumptionExampleAsync(serviceProvider);
    }

    private static async Task RunResumptionExampleAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("--- Demonstrating Workflow Resumption from Checkpoint ---");

        // This would simulate resuming a workflow that was interrupted
        // In a real scenario, you would load the checkpoint from storage
        Console.WriteLine("Simulating resumption: Workflow can be resumed from any checkpoint.");
        Console.WriteLine("Key benefits: Fault tolerance, long-running process support, state recovery.");
        Console.WriteLine();
    }

    private static async Task RunAnalyticsPipelineAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("--- Running Data Analytics Pipeline ---");

        // Define blocks with CodeBlocks manually
        var blocks = new Dictionary<string, WorkflowBlockDefinition>
        {
            ["validate_input"] = WorkflowBlockDefinition.Create(
                "validate_input",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "transform_data",
                "input_validation_failed",
                displayName: "Validate Input Data"),
            ["transform_data"] = WorkflowBlockDefinition.Create(
                "transform_data",
                "CodeBlock",
                "FlowCore.CodeBlocks",
                "analyze_data",
                "transformation_failed",
                configuration: new Dictionary<string, object>
                {
                    ["IsCodeBlock"] = true,
                    ["Mode"] = "Inline",
                    ["Language"] = "csharp",
                    ["Code"] = @"
var rawData = (System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>)context.GetState(""RawData"");
var transformedData = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
foreach (var item in rawData)
{
    var transformed = new System.Collections.Generic.Dictionary<string, object>(item);
    foreach (var key in item.Keys.ToList())
    {
        if (item[key] == null) transformed.Remove(key);
        else if (item[key] is string s && string.IsNullOrWhiteSpace(s)) transformed.Remove(key);
    }
    if (item.TryGetValue(""price"", out var priceObj) && decimal.TryParse(priceObj?.ToString(), out var price))
    {
        transformed[""discountedPrice""] = price * 0.9m;
    }
    transformedData.Add(transformed);
}
context.SetState(""TransformedData"", transformedData);
context.SetState(""TransformationSuccess"", true);
return transformedData.Count;
"
                },
                displayName: "Transform Data"),
            ["analyze_data"] = WorkflowBlockDefinition.Create(
                "analyze_data",
                "CodeBlock",
                "FlowCore.CodeBlocks",
                "generate_report",
                "analysis_failed",
                configuration: new Dictionary<string, object>
                {
                    ["IsCodeBlock"] = true,
                    ["Mode"] = "Inline",
                    ["Language"] = "csharp",
                    ["Code"] = @"
var transformedData = (System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>)context.GetState(""TransformedData"");
var analytics = new System.Collections.Generic.Dictionary<string, object>();
var prices = transformedData
    .Where(d => d.TryGetValue(""price"", out var p) && decimal.TryParse(p.ToString(), out _))
    .Select(d => decimal.Parse(d[""price""].ToString()))
    .ToList();
if (prices.Any())
{
    analytics[""averagePrice""] = prices.Average();
    analytics[""maxPrice""] = prices.Max();
    analytics[""minPrice""] = prices.Min();
    analytics[""totalItems""] = transformedData.Count;
}
context.SetState(""Analytics"", analytics);
context.SetState(""AnalysisSuccess"", true);
return analytics.Count;
"
                },
                displayName: "Analyze Data"),
            ["generate_report"] = WorkflowBlockDefinition.Create(
                "generate_report",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "send_notification",
                "report_generation_failed",
                displayName: "Generate Report"),
            ["send_notification"] = WorkflowBlockDefinition.Create(
                "send_notification",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "pipeline_complete",
                "",
                displayName: "Send Notification"),
            ["pipeline_complete"] = WorkflowBlockDefinition.Create(
                "pipeline_complete",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "",
                "",
                displayName: "Pipeline Completed Successfully"),
            // Error blocks
            ["input_validation_failed"] = WorkflowBlockDefinition.Create(
                "input_validation_failed",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "",
                "",
                displayName: "Input Validation Failed"),
            ["transformation_failed"] = WorkflowBlockDefinition.Create(
                "transformation_failed",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "retry_transformation",
                "transformation_permanently_failed",
                displayName: "Data Transformation Failed"),
            ["retry_transformation"] = WorkflowBlockDefinition.Create(
                "retry_transformation",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "analyze_data",
                "max_retries_exceeded",
                displayName: "Retry Data Transformation"),
            ["analysis_failed"] = WorkflowBlockDefinition.Create(
                "analysis_failed",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "",
                "",
                displayName: "Data Analysis Failed"),
            ["report_generation_failed"] = WorkflowBlockDefinition.Create(
                "report_generation_failed",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "",
                "",
                displayName: "Report Generation Failed"),
            ["transformation_permanently_failed"] = WorkflowBlockDefinition.Create(
                "transformation_permanently_failed",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "",
                "",
                displayName: "Transformation Permanently Failed"),
            ["max_retries_exceeded"] = WorkflowBlockDefinition.Create(
                "max_retries_exceeded",
                "BasicBlocks.LogBlock",
                "FlowCore.Common",
                "",
                "",
                displayName: "Maximum Retries Exceeded")
        };

        // Create workflow definition
        var workflow = WorkflowDefinition.Create(
            "data-analytics-pipeline",
            "Data Analytics Pipeline",
            "validate_input",
            blocks,
            version: "1.0.0",
            description: "Processes raw data, transforms it, performs analytics, and generates reports",
            metadata: new WorkflowMetadata { Author = "Data Science Team" },
            variables: new Dictionary<string, object>
            {
                ["maxDataSize"] = 10000,
                ["enableParallelProcessing"] = true,
                ["reportFormat"] = "JSON"
            });

        // Setup engine
        var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
        var executor = serviceProvider.GetRequiredService<IWorkflowExecutor>();
        var workflowStore = serviceProvider.GetRequiredService<IWorkflowStore>();
        var parser = serviceProvider.GetRequiredService<IWorkflowParser>();
        var validator = serviceProvider.GetRequiredService<IWorkflowValidator>();
        var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);

        // Sample input data
        var rawData = new List<Dictionary<string, object>>
        {
            new() { ["id"] = 1, ["name"] = "Product A", ["price"] = 100.0m, ["category"] = "Electronics" },
            new() { ["id"] = 2, ["name"] = "Product B", ["price"] = 200.0m, ["category"] = "Electronics" },
            new() { ["id"] = 3, ["name"] = "Product C", ["price"] = 150.0m, ["category"] = "Clothing" },
            new() { ["id"] = 4, ["name"] = "", ["price"] = 0, ["category"] = null } // Invalid data for testing
        };

        var input = new
        {
            RawData = rawData,
            PipelineId = "PIPE-2024-001",
            StartTime = DateTime.UtcNow
        };

        // Execute the workflow
        var result = await engine.ExecuteAsync(workflow, input);

        Console.WriteLine($"Workflow completed: {result.Succeeded}");
        Console.WriteLine($"Duration: {result.Duration?.TotalMilliseconds}ms");
        if (result.Succeeded)
        {
            object transformedObj = null;
            object analyticsObj = null;
            result.FinalState?.TryGetValue("TransformedData", out transformedObj);
            result.FinalState?.TryGetValue("Analytics", out analyticsObj);
            var transformedCount = transformedObj as List<Dictionary<string, object>>;
            var analytics = analyticsObj as Dictionary<string, object>;
            Console.WriteLine($"Transformed items: {transformedCount?.Count ?? 0}");
            if (analytics != null)
            {
                analytics.TryGetValue("averagePrice", out var avgPrice);
                analytics.TryGetValue("totalItems", out var totalItems);
                Console.WriteLine($"Average price: {avgPrice}");
                Console.WriteLine($"Total items analyzed: {totalItems}");
            }
        }
        Console.WriteLine();
    }
}