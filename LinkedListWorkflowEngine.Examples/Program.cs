namespace LinkedListWorkflowEngine.Examples;
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Linked-List-Style Workflow Engine Examples");
        Console.WriteLine("=============================================");
        Console.WriteLine();
        var serviceProvider = ConfigureServices();
        await RunBasicWorkflowExample(serviceProvider);
        await RunGuardedWorkflowExample(serviceProvider);
        await RunParallelWorkflowExample(serviceProvider);
        await RunAdvancedWorkflowExample(serviceProvider);
        await RunEcommerceWorkflowExample(serviceProvider);
        await RunErrorHandlingWorkflowExample(serviceProvider);
        await RunGuardExamples(serviceProvider);
        Console.WriteLine("All examples completed successfully!");
    }
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<IWorkflowBlockFactory, WorkflowBlockFactory>();
        return services.BuildServiceProvider();
    }
    private static async Task RunBasicWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 1: Basic Workflow");
        Console.WriteLine("-----------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("user-registration", "User Registration")
                .WithVersion("1.0.0")
                .WithDescription("Simple user registration workflow")
                .WithAuthor("Workflow Engine Team")
                .WithTags("example", "user-management")
                .StartWith("BasicBlocks.LogBlock", "welcome")
                    .OnSuccessGoTo("validate_email")
                    .WithDisplayName("Welcome Message")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validate_email")
                    .OnSuccessGoTo("send_confirmation")
                    .WithDisplayName("Email Validation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_confirmation")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Send Confirmation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Complete Registration")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);
            var input = new { UserName = "John Doe", Email = "john@example.com" };
            var result = await engine.ExecuteAsync(workflowDefinition, input);
            Console.WriteLine($"Workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Final state: {result.FinalState?.Count} items");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunGuardedWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 2: Guarded Workflow");
        Console.WriteLine("-------------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("order-processing", "Order Processing")
                .WithVersion("1.0.0")
                .WithDescription("Order processing with business rule validation")
                .WithAuthor("Business Team")
                .WithVariable("minOrderAmount", 10.0)
                .WithVariable("maxOrderAmount", 10000.0)
                .StartWith("BasicBlocks.LogBlock", "start_processing")
                    .OnSuccessGoTo("validate_order")
                    .WithDisplayName("Start Processing")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validate_order")
                    .OnSuccessGoTo("process_payment")
                    .OnFailureGoTo("reject_order")
                    .WithDisplayName("Validate Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_payment")
                    .OnSuccessGoTo("fulfill_order")
                    .OnFailureGoTo("payment_failed")
                    .WithDisplayName("Process Payment")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "fulfill_order")
                    .OnSuccessGoTo("send_confirmation")
                    .WithDisplayName("Fulfill Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_confirmation")
                    .WithDisplayName("Send Confirmation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "reject_order")
                    .WithDisplayName("Reject Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "payment_failed")
                    .WithDisplayName("Payment Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);
            var validOrder = new { Amount = 150.0, CustomerId = "CUST001" };
            var result = await engine.ExecuteAsync(workflowDefinition, validOrder);
            Console.WriteLine($"Valid order processed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunParallelWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 3: Parallel Workflow");
        Console.WriteLine("--------------------------------");
        try
        {
            var parallelBlocks = new[]
            {
                "validate_customer",
                "check_inventory",
                "calculate_shipping"
            };
            var workflowDefinition = new WorkflowBuilder("parallel-processing", "Parallel Processing")
                .WithVersion("1.0.0")
                .WithDescription("Demonstrates parallel block execution")
                .StartWith("BasicBlocks.LogBlock", "start_parallel")
                    .OnSuccessGoTo("parallel_validation")
                    .WithDisplayName("Start Parallel Processing")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "parallel_validation")
                    .OnSuccessGoTo("aggregate_results")
                    .OnFailureGoTo("validation_failed")
                    .WithDisplayName("Parallel Validation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "aggregate_results")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Aggregate Results")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("All Validations Passed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validation_failed")
                    .WithDisplayName("Some Validations Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);
            var input = new
            {
                CustomerId = "CUST001",
                ProductId = "PROD001",
                Quantity = 2
            };
            var result = await engine.ExecuteAsync(workflowDefinition, input);
            Console.WriteLine($"Parallel workflow completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunAdvancedWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 4: Advanced Workflow with State Management");
        Console.WriteLine("----------------------------------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("advanced-state-workflow", "Advanced State Management")
                .WithVersion("2.0.0")
                .WithDescription("Demonstrates state management, conditional logic, and variables")
                .WithAuthor("Advanced Workflow Team")
                .WithVariable("defaultTimeout", 5000)
                .WithVariable("maxRetries", 3)
                .StartWith("BasicBlocks.LogBlock", "initialize")
                    .OnSuccessGoTo("check_conditions")
                    .WithDisplayName("Initialize State")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "check_conditions")
                    .OnSuccessGoTo("process_data")
                    .WithDisplayName("Condition Check")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_data")
                    .OnSuccessGoTo("wait_and_complete")
                    .WithDisplayName("Data Processing")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "wait_and_complete")
                    .OnSuccessGoTo("finalize")
                    .WithDisplayName("Wait Step")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "finalize")
                    .WithDisplayName("Finalize Workflow")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "skip_processing")
                    .WithDisplayName("Skip Processing")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var input = new
            {
                ShouldProcess = true,
                DataSize = 100,
                UserId = "user123"
            };

            var result = await engine.ExecuteAsync(workflowDefinition, input);
            Console.WriteLine($"Advanced workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Final state contains {result.FinalState?.Count} items");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunEcommerceWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 5: E-commerce Order Processing");
        Console.WriteLine("------------------------------------------");
        try
        {
            var parallelValidationBlocks = new[]
            {
                "validate_inventory",
                "validate_payment",
                "validate_shipping"
            };

            var workflowDefinition = new WorkflowBuilder("ecommerce-order", "E-commerce Order Processing")
                .WithVersion("3.0.0")
                .WithDescription("Complete order processing with parallel validation and business rules")
                .WithAuthor("E-commerce Team")
                .WithVariable("minOrderAmount", 10.0)
                .WithVariable("maxOrderAmount", 10000.0)
                .WithVariable("businessHoursStart", "09:00")
                .WithVariable("businessHoursEnd", "17:00")
                .StartWith("BasicBlocks.LogBlock", "start_order")
                    .OnSuccessGoTo("validate_order")
                    .WithDisplayName("Start Order Processing")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validate_order")
                    .OnSuccessGoTo("parallel_validation")
                    .WithDisplayName("Order Validation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "parallel_validation")
                    .OnSuccessGoTo("process_order")
                    .WithDisplayName("Parallel Validation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_order")
                    .OnSuccessGoTo("send_confirmation")
                    .WithDisplayName("Order Processing")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_confirmation")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Send Confirmation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Order Complete")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "order_failed")
                    .WithDisplayName("Order Failed")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var orderInput = new
            {
                OrderId = "ORD-2024-001",
                CustomerId = "CUST-456",
                Amount = 299.99,
                Items = new[] { "ITEM-1", "ITEM-2", "ITEM-3" },
                ShippingAddress = "123 Main St, City, State 12345"
            };

            var result = await engine.ExecuteAsync(workflowDefinition, orderInput);
            Console.WriteLine($"E-commerce workflow completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunErrorHandlingWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 6: Error Handling and Recovery");
        Console.WriteLine("-------------------------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("error-handling-workflow", "Error Handling Demo")
                .WithVersion("1.0.0")
                .WithDescription("Demonstrates comprehensive error handling and recovery mechanisms")
                .WithAuthor("Reliability Team")
                .StartWith("BasicBlocks.LogBlock", "start_demo")
                    .OnSuccessGoTo("simulate_work")
                    .WithDisplayName("Start Error Demo")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "simulate_work")
                    .OnSuccessGoTo("check_status")
                    .OnFailureGoTo("error_occurred")
                    .WithDisplayName("Simulate Work")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "check_status")
                    .OnSuccessGoTo("success_path")
                    .OnFailureGoTo("recovery_attempt")
                    .WithDisplayName("Status Check")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "recovery_attempt")
                    .OnSuccessGoTo("recovery_success")
                    .OnFailureGoTo("critical_error")
                    .WithDisplayName("Recovery Attempt")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "recovery_success")
                    .OnSuccessGoTo("final_success")
                    .WithDisplayName("Recovery Success")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "final_success")
                    .WithDisplayName("Final Success")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "error_occurred")
                    .WithDisplayName("Error Handler")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var input = new
            {
                TestFailure = true,
                RetryCount = 1,
                SimulateError = true
            };

            var result = await engine.ExecuteAsync(workflowDefinition, input);
            Console.WriteLine($"Error handling workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Final status: {result.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunGuardExamples(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 7: Guard Validation Examples");
        Console.WriteLine("------------------------------------");

        await RunBusinessHoursGuardExample(serviceProvider);
        await RunDataValidationGuardExample(serviceProvider);
        await RunNumericRangeGuardExample(serviceProvider);
        await RunRequiredFieldGuardExample(serviceProvider);
        await RunAuthorizationGuardExample(serviceProvider);
    }

    private static async Task RunBusinessHoursGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Business Hours Guard Example");
        Console.WriteLine("----------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("business-hours-workflow", "Business Hours Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates business hours before processing")
                .WithAuthor("Operations Team")
                .StartWith("BasicBlocks.LogBlock", "check_time")
                    .OnSuccessGoTo("process_request")
                    .OnFailureGoTo("outside_hours")
                    .WithDisplayName("Check Current Time")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_request")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Process Request")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Request Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "outside_hours")
                    .WithDisplayName("Outside Business Hours")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var result = await engine.ExecuteAsync(workflowDefinition, new { });
            Console.WriteLine($"Business hours validation completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunDataValidationGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Data Validation Guard Example");
        Console.WriteLine("-----------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("data-validation-workflow", "Data Format Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates email format and required fields")
                .WithAuthor("Data Quality Team")
                .StartWith("BasicBlocks.LogBlock", "validate_input")
                    .OnSuccessGoTo("process_data")
                    .OnFailureGoTo("validation_failed")
                    .WithDisplayName("Validate Input Data")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_data")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Process Valid Data")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Data Processing Complete")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validation_failed")
                    .WithDisplayName("Data Validation Failed")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var validInput = new
            {
                Email = "user@example.com",
                CustomerId = "CUST-123",
                Phone = "+1-555-0123"
            };

            var result = await engine.ExecuteAsync(workflowDefinition, validInput);
            Console.WriteLine($"Data validation completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunNumericRangeGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Numeric Range Guard Example");
        Console.WriteLine("---------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("numeric-validation-workflow", "Numeric Range Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates numeric ranges for business rules")
                .WithAuthor("Business Rules Team")
                .WithVariable("minAmount", 10.0)
                .WithVariable("maxAmount", 5000.0)
                .StartWith("BasicBlocks.LogBlock", "validate_amount")
                    .OnSuccessGoTo("process_payment")
                    .OnFailureGoTo("amount_rejected")
                    .WithDisplayName("Validate Amount Range")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_payment")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Process Payment")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Payment Processed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "amount_rejected")
                    .WithDisplayName("Amount Outside Valid Range")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var testInput = new
            {
                Amount = 150.75,
                Currency = "USD",
                MerchantId = "MERCH-456"
            };

            var result = await engine.ExecuteAsync(workflowDefinition, testInput);
            Console.WriteLine($"Numeric validation completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunRequiredFieldGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Required Field Guard Example");
        Console.WriteLine("----------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("required-fields-workflow", "Required Fields Validation")
                .WithVersion("1.0.0")
                .WithDescription("Ensures all required fields are present")
                .WithAuthor("Data Integrity Team")
                .StartWith("BasicBlocks.LogBlock", "check_required")
                    .OnSuccessGoTo("process_complete")
                    .OnFailureGoTo("missing_fields")
                    .WithDisplayName("Check Required Fields")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_complete")
                    .OnSuccessGoTo("finalize")
                    .WithDisplayName("Process Complete Data")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "finalize")
                    .WithDisplayName("Processing Finalized")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "missing_fields")
                    .WithDisplayName("Required Fields Missing")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var completeInput = new
            {
                CustomerId = "CUST-999",
                Email = "complete@example.com",
                FirstName = "John",
                LastName = "Doe",
                Address = "123 Main St"
            };

            var result = await engine.ExecuteAsync(workflowDefinition, completeInput);
            Console.WriteLine($"Required fields validation completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task RunAuthorizationGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Authorization Guard Example");
        Console.WriteLine("---------------------------");
        try
        {
            var workflowDefinition = new WorkflowBuilder("authorization-workflow", "Authorization Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates user permissions and roles")
                .WithAuthor("Security Team")
                .StartWith("BasicBlocks.LogBlock", "check_permissions")
                    .OnSuccessGoTo("access_granted")
                    .OnFailureGoTo("access_denied")
                    .WithDisplayName("Check User Permissions")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "access_granted")
                    .OnSuccessGoTo("perform_action")
                    .WithDisplayName("Access Granted")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "perform_action")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Perform Authorized Action")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Action Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "access_denied")
                    .WithDisplayName("Access Denied")
                    .And()
                .Build();

            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var engine = new WorkflowEngine(serviceProvider, logger);

            var authorizedInput = new
            {
                UserId = "user-admin",
                Permissions = new[] { "read", "write", "admin" },
                Roles = new[] { "administrator", "manager" },
                Action = "delete_user"
            };

            var result = await engine.ExecuteAsync(workflowDefinition, authorizedInput);
            Console.WriteLine($"Authorization validation completed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
}
public static class WorkflowBuilderExtensions
{
    public static WorkflowBuilder.WorkflowBlockBuilder StartWith<TBlock>(
        this WorkflowBuilder builder, string message)
        where TBlock : IWorkflowBlock
    {
        var block = CreateBlockInstance<TBlock>(message) as IWorkflowBlock;
        if (block == null)
        {
            throw new InvalidOperationException($"Failed to create block of type {typeof(TBlock)}");
        }
        return builder.StartWith(block);
    }
    public static WorkflowBuilder.WorkflowBlockBuilder AddBlock<TBlock>(
        this WorkflowBuilder builder, string message)
        where TBlock : IWorkflowBlock
    {
        var block = CreateBlockInstance<TBlock>(message) as IWorkflowBlock;
        if (block == null)
        {
            throw new InvalidOperationException($"Failed to create block of type {typeof(TBlock)}");
        }
        return builder.AddBlock(block);
    }
    private static object CreateBlockInstance<TBlock>(string message)
    {
        var blockType = typeof(TBlock);

        if (blockType == typeof(BasicBlocks.LogBlock))
        {
            return new BasicBlocks.LogBlock(message, nextBlockOnSuccess: "");
        }
        else if (blockType == typeof(BasicBlocks.WaitBlock))
        {
            return new BasicBlocks.WaitBlock(TimeSpan.FromMilliseconds(100), nextBlockOnSuccess: "");
        }
        else if (blockType == typeof(BasicBlocks.SetStateBlock))
        {
            return new BasicBlocks.SetStateBlock("message", message, nextBlockOnSuccess: "");
        }
        else if (blockType == typeof(BasicBlocks.ConditionalBlock))
        {
            return new BasicBlocks.ConditionalBlock(ctx => true, nextBlockOnConditionMet: "");
        }
        else if (blockType == typeof(BasicBlocks.FailBlock))
        {
            return new BasicBlocks.FailBlock(message, nextBlockOnSuccess: "", nextBlockOnFailure: "");
        }
        else
        {
            return Activator.CreateInstance(blockType, message)!;
        }
    }
}