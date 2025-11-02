namespace FlowCore.Examples;
/// <summary>
/// Complex example showcasing FlowCore's core strengths in a document processing pipeline.
/// Demonstrates type safety, optional execution, code execution, guards, JSON workflows, and state persistence.
/// </summary>
public static class DocumentProcessingExample
{
    /// <summary>
    /// Runs the document processing pipeline example.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== FlowCore Document Processing Pipeline Example ===");
        Console.WriteLine("Showcasing: Type Safety, Code Execution, Guards, JSON Workflows, State Persistence");
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
            // WARNING: Dynamic assembly loading is enabled for demonstration purposes only.
            // In production, carefully evaluate security implications before enabling.
            return new WorkflowBlockFactorySecurityOptions
            {
                AllowDynamicAssemblyLoading = true,
                AllowedAssemblyNames = new[] { "FlowCore" },
                ValidateStrongNameSignatures = true
            };
        });
        services.AddSingleton<WorkflowBlockFactory>(sp =>
        {
            var securityOptions = sp.GetRequiredService<WorkflowBlockFactorySecurityOptions>();
            return new WorkflowBlockFactory(sp, securityOptions);
        });
        services.AddSingleton<IWorkflowBlockFactory>(sp => sp.GetRequiredService<WorkflowBlockFactory>());
        services.AddSingleton<IWorkflowBlockFactory>(sp => sp.GetRequiredService<WorkflowBlockFactory>());
        services.AddSingleton<IStateManager, InMemoryStateManager>();
        services.AddSingleton<IWorkflowExecutor, WorkflowExecutor>();
        services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();
        services.AddSingleton<IWorkflowParser, WorkflowDefinitionParser>();
        services.AddSingleton<IWorkflowValidator, WorkflowValidator>();
        services.AddSingleton<ICodeExecutor, InlineCodeExecutor>();
        services.AddSingleton<GuardManager>();
        var serviceProvider = services.BuildServiceProvider();
        // Run code-based workflow
        await RunCodeBasedWorkflowAsync(serviceProvider);
        // Run JSON-based workflow
        await RunJsonBasedWorkflowAsync(serviceProvider);
        Console.WriteLine("Document processing pipeline completed successfully!");
    }
    private static async Task RunCodeBasedWorkflowAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("--- Code-Based Workflow ---");
        var workflow = FlowCoreWorkflowBuilder.Create("document-processing", "Document Processing Pipeline")
            .WithVersion("1.0.0")
            .WithDescription("Secure document processing with OCR and classification")
            .WithVariable("maxFileSize", 10485760L) // 10MB
            .WithVariable("supportedFormats", new[] { "PDF", "JPG", "PNG", "TIFF" })
            .WithVariable("securityLevel", "high")
            .StartWith("BasicBlocks.LogBlock", "validate_document")
                .OnSuccessGoTo("extract_text")
                .OnFailureGoTo("validation_failed")
                .WithDisplayName("Validate Document Upload")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "extract_text")
                .OnSuccessGoTo("classify_document")
                .WithDisplayName("Extract Text (OCR)")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "classify_document")
                .OnSuccessGoTo("store_document")
                .WithDisplayName("Classify Document")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "store_document")
                .OnSuccessGoTo("send_notification")
                .WithDisplayName("Store Document")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "send_notification")
                .WithDisplayName("Send Processing Notification")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "validation_failed")
                .WithDisplayName("Validation Failed")
                .And()
            .Build();
        var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
        var executor = serviceProvider.GetRequiredService<IWorkflowExecutor>();
        var workflowStore = serviceProvider.GetRequiredService<IWorkflowStore>();
        var parser = serviceProvider.GetRequiredService<IWorkflowParser>();
        var validator = serviceProvider.GetRequiredService<IWorkflowValidator>();
        var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
        var documentData = new
        {
            DocumentId = "DOC-2024-001",
            FileName = "invoice_techcorp_001.pdf",
            FileSize = 2048576L, // 2MB
            FileFormat = "PDF",
            Content = "Sample PDF content for OCR processing"
        };
        var result = await engine.ExecuteAsync(workflow, documentData);
        Console.WriteLine($"Workflow completed: {result.Succeeded}");
        Console.WriteLine($"Duration: {result.Duration?.TotalMilliseconds}ms");
        Console.WriteLine();
    }
    private static async Task RunJsonBasedWorkflowAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("--- JSON-Based Workflow ---");
        var jsonWorkflow = @"
        {
          ""id"": ""secure-document-processing"",
          ""name"": ""Secure Document Processing Pipeline"",
          ""version"": ""1.0.0"",
          ""description"": ""High-security document processing with advanced validation and code execution"",
          ""variables"": {
            ""maxFileSize"": 10485760,
            ""supportedFormats"": [""PDF"", ""JPG"", ""PNG"", ""TIFF""],
            ""securityLevel"": ""high"",
            ""requireMFA"": true,
            ""auditLevel"": ""detailed""
          },
          ""blocks"": {
            ""validate_document"": {
              ""id"": ""validate_document"",
              ""type"": ""BasicBlocks.LogBlock"",
              ""nextBlockOnSuccess"": ""extract_text"",
              ""nextBlockOnFailure"": ""validation_failed"",
              ""displayName"": ""Validate Document Upload""
            },
            ""extract_text"": {
              ""id"": ""extract_text"",
              ""type"": ""BasicBlocks.LogBlock"",
              ""nextBlockOnSuccess"": ""classify_document"",
              ""displayName"": ""Extract Text (OCR)""
            },
            ""classify_document"": {
              ""id"": ""classify_document"",
              ""type"": ""BasicBlocks.LogBlock"",
              ""nextBlockOnSuccess"": ""store_document"",
              ""displayName"": ""Classify Document""
            },
            ""store_document"": {
              ""id"": ""store_document"",
              ""type"": ""BasicBlocks.LogBlock"",
              ""nextBlockOnSuccess"": ""send_notification"",
              ""displayName"": ""Store Document""
            },
            ""send_notification"": {
              ""id"": ""send_notification"",
              ""type"": ""BasicBlocks.LogBlock"",
              ""displayName"": ""Send Processing Notification""
            },
            ""validation_failed"": {
              ""id"": ""validation_failed"",
              ""type"": ""BasicBlocks.LogBlock"",
              ""displayName"": ""Validation Failed""
            }
          },
          ""execution"": {
            ""startBlock"": ""validate_document"",
            ""timeout"": ""00:10:00"",
            ""retryPolicy"": {
              ""maxRetries"": 3,
              ""backoffStrategy"": ""exponential""
            }
          },
          ""persistence"": {
            ""provider"": ""InMemory"",
            ""checkpointFrequency"": ""AfterEachBlock""
          }
        }";
        var jsonEngine = new Parsing.JsonWorkflowEngine(serviceProvider);
        var documentData = new
        {
            DocumentId = "DOC-JSON-001",
            FileName = "contract_secure.pdf",
            FileSize = 1024000L, // 1MB
            FileFormat = "PDF",
            Content = "Secure contract document content"
        };
        var result = await jsonEngine.ExecuteFromJsonAsync(jsonWorkflow, documentData);
        Console.WriteLine($"JSON Workflow completed: {result.Succeeded}");
        Console.WriteLine($"Duration: {result.Duration?.TotalMilliseconds}ms");
        Console.WriteLine();
    }
}
