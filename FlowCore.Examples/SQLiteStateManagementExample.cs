namespace FlowCore.Examples;

using FlowCore.Persistence;

/// <summary>
/// Example demonstrating SQLite-based state management for long-running workflows.
/// </summary>
public static class SQLiteStateManagementExample
{
    /// <summary>
    /// Demonstrates SQLite state management with a long-running order processing workflow.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== SQLite State Management Example ===\n");

        // Configure dependency injection with SQLite state manager
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // Configure SQLite state manager
        var dbPath = Path.Combine(Path.GetTempPath(), "flowcore_workflows.db");
        var stateManagerConfig = new StateManagerConfig
        {
            CheckpointFrequency = CheckpointFrequency.AfterEachBlock,
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                MinSizeThreshold = 1024,  // Compress data larger than 1KB
                Algorithm = CompressionAlgorithm.GZip
            }
        };

        services.AddSingleton<IStateManager>(sp => 
            new SQLiteStateManager(dbPath, stateManagerConfig, 
                sp.GetRequiredService<ILogger<SQLiteStateManager>>()));
        services.AddSingleton<IWorkflowBlockFactory, WorkflowBlockFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
        var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
        var stateManager = serviceProvider.GetRequiredService<IStateManager>();

        Console.WriteLine($"SQLite database: {dbPath}");
        Console.WriteLine($"Compression: {stateManagerConfig.Compression.Enabled}");
        Console.WriteLine($"Checkpoint frequency: {stateManagerConfig.CheckpointFrequency}\n");

        // Example 1: Long-running order processing workflow
        await LongRunningOrderProcessingExample(blockFactory, stateManager, logger);

        // Example 2: Workflow state persistence and recovery
        await WorkflowRecoveryExample(blockFactory, stateManager, logger);

        // Example 3: State cleanup and statistics
        await StateMaintenanceExample(stateManager);

        // Cleanup
        stateManager.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
            Console.WriteLine($"\nCleaned up database: {dbPath}");
        }

        Console.WriteLine("\n=== SQLite State Management Example Complete ===\n");
    }

    private static async Task LongRunningOrderProcessingExample(
        IWorkflowBlockFactory blockFactory,
        IStateManager stateManager,
        ILogger<WorkflowEngine> logger)
    {
        Console.WriteLine("--- Long-Running Order Processing ---\n");

        var workflow = FlowCoreWorkflowBuilder.Create("order-processing", "Order Processing")
            .WithVersion("1.0.0")
            .WithDescription("Long-running order processing with state persistence")
            .WithVariable("minOrderAmount", 10.0m)
            .WithVariable("maxOrderAmount", 10000.0m)
            .StartWith("BasicBlocks.LogBlock", "validate_order")
                .OnSuccessGoTo("process_payment")
                .OnFailureGoTo("reject_order")
                .WithDisplayName("Validate Order")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "process_payment")
                .OnSuccessGoTo("update_inventory")
                .OnFailureGoTo("payment_failed")
                .WithDisplayName("Process Payment")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "update_inventory")
                .OnSuccessGoTo("send_confirmation")
                .WithDisplayName("Update Inventory")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "send_confirmation")
                .WithDisplayName("Send Order Confirmation")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "reject_order")
                .WithDisplayName("Reject Order")
                .And()
            .AddBlock("BasicBlocks.LogBlock", "payment_failed")
                .WithDisplayName("Payment Failed - Notify Customer")
                .And()
            .Build();

        var orderData = new
        {
            OrderId = "ORD-2024-12345",
            CustomerId = "CUST-PREMIUM-001",
            Amount = 1299.99m,
            Items = new[]
            {
                new { ProductId = "PRD-001", Name = "Gaming Laptop", Quantity = 1, Price = 1199.99m },
                new { ProductId = "PRD-002", Name = "Wireless Mouse", Quantity = 1, Price = 49.99m },
                new { ProductId = "PRD-003", Name = "USB-C Cable", Quantity = 2, Price = 24.99m }
            },
            ShippingAddress = new
            {
                Street = "123 Tech Ave",
                City = "San Francisco",
                State = "CA",
                ZipCode = "94102",
                Country = "USA"
            }
        };

        // Create engine components
        var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
        var workflowStore = new InMemoryWorkflowStore();
        var parser = new WorkflowDefinitionParser();
        var validator = new WorkflowValidator();
        var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);

        Console.WriteLine($"Order ID: {orderData.OrderId}");
        Console.WriteLine($"Order Amount: ${orderData.Amount}\n");

        var result = await engine.ExecuteAsync(workflow, orderData);

        Console.WriteLine($"Workflow completed: {result.Succeeded}");
        Console.WriteLine($"Duration: {result.Duration?.TotalMilliseconds}ms\n");

        // Demonstrate state management directly
        var executionId = Guid.NewGuid();
        var state = new Dictionary<string, object>
        {
            ["orderId"] = orderData.OrderId,
            ["amount"] = orderData.Amount,
            ["status"] = "completed"
        };

        await stateManager.SaveStateAsync(workflow.Id, executionId, state);
        Console.WriteLine($"State saved for execution: {executionId}");
        
        var stateExists = await stateManager.StateExistsAsync(workflow.Id, executionId);
        Console.WriteLine($"State persisted to database: {stateExists}");

        if (stateExists)
        {
            var metadata = await stateManager.GetStateMetadataAsync(workflow.Id, executionId);
            if (metadata != null)
            {
                Console.WriteLine($"Workflow status: {metadata.Status}");
                Console.WriteLine($"State size: {metadata.StateSize} bytes");
                Console.WriteLine($"Last updated: {metadata.UpdatedAt:yyyy-MM-dd HH:mm:ss}\n");
            }
        }
    }

    private static async Task WorkflowRecoveryExample(
        IWorkflowBlockFactory blockFactory,
        IStateManager stateManager,
        ILogger<WorkflowEngine> logger)
    {
        Console.WriteLine("--- Workflow State Recovery Example ---\n");

        var workflowId = "recoverable-workflow";
        var executionId = Guid.NewGuid();

        // Simulate a workflow execution that saves state
        var state = new Dictionary<string, object>
        {
            ["workflowId"] = workflowId,
            ["executionId"] = executionId.ToString(),
            ["currentStep"] = "payment_processing",
            ["orderAmount"] = 599.99m,
            ["customerId"] = "CUST-001",
            ["timestamp"] = DateTime.UtcNow,
            ["attemptCount"] = 1,
            ["previousSteps"] = new List<string> { "validate_order", "check_inventory" }
        };

        var metadata = new WorkflowStateMetadata(
            workflowId,
            executionId,
            WorkflowStatus.Running,
            "payment_processing",
            0,
            "1.0.0");

        Console.WriteLine($"Saving workflow state for recovery...");
        Console.WriteLine($"Execution ID: {executionId}");
        Console.WriteLine($"Current step: {state["currentStep"]}\n");

        await stateManager.SaveStateAsync(workflowId, executionId, state, metadata);

        // Simulate recovery: Load the state
        Console.WriteLine("Simulating workflow recovery after interruption...\n");

        var recoveredState = await stateManager.LoadStateAsync(workflowId, executionId);
        if (recoveredState != null)
        {
            Console.WriteLine("Successfully recovered workflow state:");
            Console.WriteLine($"  Current step: {recoveredState["currentStep"]}");
            Console.WriteLine($"  Order amount: ${recoveredState["orderAmount"]}");
            Console.WriteLine($"  Attempt count: {recoveredState["attemptCount"]}");
            
            var previousSteps = recoveredState["previousSteps"] as List<object>;
            if (previousSteps != null)
            {
                Console.WriteLine($"  Previous steps: {string.Join(", ", previousSteps)}");
            }
            
            Console.WriteLine("\nWorkflow can resume from the saved checkpoint.\n");
        }

        // Update metadata to mark as completed
        metadata.UpdateStatus(WorkflowStatus.Completed);
        await stateManager.UpdateStateMetadataAsync(workflowId, executionId, metadata);
        
        var updatedMetadata = await stateManager.GetStateMetadataAsync(workflowId, executionId);
        Console.WriteLine($"Updated workflow status: {updatedMetadata?.Status}\n");
    }

    private static async Task StateMaintenanceExample(IStateManager stateManager)
    {
        Console.WriteLine("--- State Maintenance Example ---\n");

        // Get statistics before cleanup
        var statsBefore = await stateManager.GetStatisticsAsync();
        Console.WriteLine("Current database statistics:");
        Console.WriteLine($"  Total states: {statsBefore.TotalStates}");
        Console.WriteLine($"  Total size: {statsBefore.TotalSizeBytes:N0} bytes");
        Console.WriteLine($"  Active executions: {statsBefore.ActiveExecutions}");
        Console.WriteLine($"  Completed executions: {statsBefore.CompletedExecutions}");
        Console.WriteLine($"  Failed executions: {statsBefore.FailedExecutions}\n");

        // Cleanup old completed workflows (older than 30 days)
        var cleanupDate = DateTime.UtcNow.AddDays(-30);
        Console.WriteLine($"Cleaning up completed workflows older than: {cleanupDate:yyyy-MM-dd}");
        
        var deletedCount = await stateManager.CleanupOldStatesAsync(
            cleanupDate,
            status: WorkflowStatus.Completed);
        
        Console.WriteLine($"Deleted {deletedCount} old workflow states\n");

        // Get statistics after cleanup
        var statsAfter = await stateManager.GetStatisticsAsync();
        Console.WriteLine("Statistics after cleanup:");
        Console.WriteLine($"  Total states: {statsAfter.TotalStates}");
        Console.WriteLine($"  Total size: {statsAfter.TotalSizeBytes:N0} bytes");
        Console.WriteLine($"  Space saved: {statsBefore.TotalSizeBytes - statsAfter.TotalSizeBytes:N0} bytes\n");
    }

    /// <summary>
    /// Production configuration example with encryption and compression.
    /// </summary>
    public static void ShowProductionConfiguration()
    {
        Console.WriteLine("=== Production SQLite Configuration ===\n");

        var productionConfig = new StateManagerConfig
        {
            // Enable automatic checkpointing after each block
            CheckpointFrequency = CheckpointFrequency.AfterEachBlock,

            // Enable compression for large state data
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                MinSizeThreshold = 2048,  // 2KB threshold
                Algorithm = CompressionAlgorithm.GZip
            },

            // Enable encryption for sensitive data
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = "production-key-2024",
                Algorithm = EncryptionAlgorithm.AES256
            },

            // State retention and cleanup
            MaxStateAge = TimeSpan.FromDays(90),

            // Enable versioning for state history
            EnableVersioning = true,
            MaxVersionsPerExecution = 5
        };

        Console.WriteLine("Production configuration settings:");
        Console.WriteLine($"  Checkpoint frequency: {productionConfig.CheckpointFrequency}");
        Console.WriteLine($"  Compression enabled: {productionConfig.Compression.Enabled}");
        Console.WriteLine($"  Compression threshold: {productionConfig.Compression.MinSizeThreshold} bytes");
        Console.WriteLine($"  Encryption enabled: {productionConfig.Encryption.Enabled}");
        Console.WriteLine($"  Encryption algorithm: {productionConfig.Encryption.Algorithm}");
        Console.WriteLine($"  Max state age: {productionConfig.MaxStateAge.TotalDays} days");
        Console.WriteLine($"  Versioning enabled: {productionConfig.EnableVersioning}");
        Console.WriteLine($"  Max versions per execution: {productionConfig.MaxVersionsPerExecution}\n");

        Console.WriteLine("Best practices:");
        Console.WriteLine("  1. Use encryption for sensitive workflow data");
        Console.WriteLine("  2. Enable compression to reduce storage costs");
        Console.WriteLine("  3. Implement regular cleanup of old states");
        Console.WriteLine("  4. Monitor state size and database growth");
        Console.WriteLine("  5. Use connection pooling for high-throughput scenarios");
        Console.WriteLine("  6. Regular database backups for disaster recovery");
        Console.WriteLine("  7. Consider sharding for very large deployments\n");

        Console.WriteLine("=== Production Configuration Complete ===\n");
    }
}
