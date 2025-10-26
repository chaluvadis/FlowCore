# Linked-List-Style Workflow Engine - Usage Examples

This document provides comprehensive examples and documentation for using the Linked-List-Style Workflow Engine for .NET.

## 🚀 Quick Start

### Basic Workflow

```csharp
using LinkedListWorkflowEngine.Core;
using LinkedListWorkflowEngine.Core.Common;
using Microsoft.Extensions.DependencyInjection;

// Set up services
var services = new ServiceCollection();
services.AddLogging();
var serviceProvider = services.BuildServiceProvider();

// Create a simple workflow
var workflowDefinition = FlowCoreWorkflowBuilder.Create("my-workflow", "My First Workflow")
    .WithVersion("1.0.0")
    .WithDescription("A simple demonstration workflow")
    .StartWith<BasicBlocks.LogBlock>("Starting workflow...")
        .OnSuccessGoTo("process_data")
        .And()
    .AddBlock<BasicBlocks.LogBlock>("Processing data...")
        .OnSuccessGoTo("complete")
        .And()
    .AddBlock<BasicBlocks.LogBlock>("Workflow complete!")
        .And()
    .Build();

// Execute the workflow
var engine = new WorkflowEngine(serviceProvider);
var result = await engine.ExecuteAsync(workflowDefinition, new { UserId = 123 });

Console.WriteLine($"Workflow completed in {result.Duration?.TotalMilliseconds}ms");
```

## 📖 Comprehensive Examples

The `FlowCore.Examples` project includes comprehensive examples demonstrating various features of the workflow engine. Run the examples to see the engine in action:

```bash
cd FlowCore.Examples
dotnet run
```

### Example 10: Data Analytics Pipeline

A complex example showcasing FlowCore's advanced capabilities in a real-world data processing scenario.

**Features Demonstrated:**
- **Dynamic Code Execution**: Runtime C# code for data transformation and analytics
- **Workflow Orchestration**: Multi-step pipeline with conditional transitions
- **Error Handling & Recovery**: Retry mechanisms and graceful failure handling
- **State Persistence**: Data flow and state management between blocks
- **Security**: Secure code execution with namespace restrictions

**Pipeline Structure:**
```
Input Validation → Data Transformation (CodeBlock) → Analytics (CodeBlock) → Report Generation → Notification
                      ↓ (Error Path)
                 Retry Logic → Permanent Failure
```

**Key Components:**
- **Transform Data Block**: Cleans and enriches raw data, calculates discounts
- **Analyze Data Block**: Computes statistics (averages, min/max, totals)
- **Error Recovery**: Automatic retries with fallback to permanent failure
- **State Management**: Seamless data passing between execution steps

**Usage:**
```csharp
// Run the complete example suite
await DataAnalyticsPipelineExample.RunAsync();
```

This example processes sample product data, demonstrates error handling with invalid entries, and showcases the engine's ability to handle complex business logic through dynamic code execution while maintaining security and reliability.

## 📚 Core Concepts

### Workflow Blocks

Workflow blocks are the building blocks of your workflow. Each block implements `IWorkflowBlock` and performs a specific operation.

```csharp
public class MyCustomBlock : WorkflowBlockBase
{
    public override string NextBlockOnSuccess { get; }
    public override string NextBlockOnFailure { get; }

    public MyCustomBlock(string nextBlockOnSuccess = "", string nextBlockOnFailure = "")
        : base(logger)
    {
        NextBlockOnSuccess = nextBlockOnSuccess;
        NextBlockOnFailure = nextBlockOnFailure;
    }

    protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
    {
        // Your custom logic here
        LogInfo("Executing custom business logic");

        // Access input data
        var input = context.Input;

        // Store results in state
        context.SetState("processed", true);

        // Return success or failure
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}
```

### Execution Context

The `ExecutionContext` provides access to workflow data and services:

```csharp
protected override async Task<ExecutionResult> ExecuteBlockAsync(ExecutionContext context)
{
    // Access input data
    var userId = context.GetState<int>("userId", 0);

    // Store data for other blocks
    context.SetState("processedAt", DateTime.UtcNow);

    // Get services from DI container
    var myService = context.GetService<IMyService>();

    // Check for cancellation
    context.ThrowIfCancellationRequested();

    return ExecutionResult.Success("next_block");
}
```

### Execution Results

Control workflow flow with different execution outcomes:

```csharp
// Continue to next block
return ExecutionResult.Success("next_block");

// Skip to alternative path
return ExecutionResult.Skip("alternative_block", "Condition not met");

// Handle errors
return ExecutionResult.Failure("error_handler", null, exception);

// Wait before continuing
return ExecutionResult.Wait(TimeSpan.FromMinutes(5), "continue_after_wait");
```

## 🛡️ Guard Validation

Protect your workflows with pre and post-execution validation:

```csharp
// Using common guards
var businessHoursGuard = new CommonGuards.BusinessHoursGuard(
    TimeSpan.FromHours(9),
    TimeSpan.FromHours(17));

var emailGuard = new CommonGuards.DataFormatGuard(
    "email",
    @"^[^@]+@[^@]+\.[^@]+$");

var amountGuard = new CommonGuards.NumericRangeGuard(
    "amount", 10, 1000);

// Custom guard implementation
public class CustomValidationGuard : IGuard
{
    public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
    {
        var data = context.GetState<MyData>("data");

        if (data.IsValid)
        {
            return GuardResult.Success();
        }

        return GuardResult.Failure(
            "Data validation failed",
            "invalid_data_handler",
            GuardSeverity.Error);
    }
}
```

## ⚡ Parallel Execution

Execute multiple blocks concurrently for improved performance:

```csharp
var parallelBlock = new ParallelBlock(
    new[] { "validate_user", "check_permissions", "load_profile" },
    ParallelExecutionMode.All, // All must succeed
    "aggregation_block",
    "error_handler");
```

## 💾 State Persistence

Enable long-running workflow support with state persistence:

```csharp
// Configure state manager
var stateManager = new InMemoryStateManager();
var persistenceService = new WorkflowStatePersistenceService(stateManager);

// Create engine with persistence
var engine = new WorkflowEngine(serviceProvider, logger, stateManager: stateManager);

// Workflow state is automatically saved at checkpoints
var result = await engine.ExecuteAsync(workflowDefinition, input);

// Resume from checkpoint if needed
var checkpoint = await persistenceService.LoadLatestCheckpointAsync(workflowId, executionId);
```

## 🔧 Advanced Configuration

### Workflow Definition with Guards

```csharp
var workflowDefinition = WorkflowDefinition.Create(
    "enterprise-workflow",
    "Enterprise Workflow",
    "start_block",
    blocks,
    version: "2.0.0",
    description: "Enterprise-grade workflow with validation",
    globalGuards: new[]
    {
        GuardDefinition.CreatePreExecution(
            "auth_guard",
            "AuthorizationGuard",
            "MyAssembly",
            severity: GuardSeverity.Critical)
    });
```

### Custom Block Factory

```csharp
public class CustomBlockFactory : IWorkflowBlockFactory
{
    private readonly IServiceProvider _serviceProvider;

    public IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition)
    {
        // Custom block creation logic
        var blockType = Type.GetType(blockDefinition.BlockType);
        return Activator.CreateInstance(blockType, _serviceProvider) as IWorkflowBlock;
    }
}
```

## 📊 Monitoring and Analytics

Track workflow execution and performance:

```csharp
var result = await engine.ExecuteAsync(workflowDefinition, input);

// Access execution metadata
Console.WriteLine($"Duration: {result.Duration}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"State Size: {result.FinalState?.Count} items");

// Get state manager statistics
var stats = await stateManager.GetStatisticsAsync();
Console.WriteLine($"Total States: {stats.TotalStates}");
Console.WriteLine($"Average State Size: {stats.AverageStateSize} bytes");
```

## 🧪 Testing

Write unit tests for your workflows:

```csharp
[Fact]
public async Task MyWorkflow_Should_Process_Correctly()
{
    // Arrange
    var engine = new WorkflowEngine(serviceProvider);
    var workflow = CreateTestWorkflow();

    // Act
    var result = await engine.ExecuteAsync(workflow, testInput);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Equal(WorkflowStatus.Completed, result.Status);
    Assert.NotNull(result.FinalState);
}
```

## 🚨 Error Handling

Implement comprehensive error handling:

```csharp
try
{
    var result = await engine.ExecuteAsync(workflowDefinition, input);

    if (!result.Succeeded)
    {
        Console.WriteLine($"Workflow failed: {result.Error?.Message}");

        // Handle specific failure types
        switch (result.Status)
        {
            case WorkflowStatus.Failed:
                // Handle permanent failures
                break;
            case WorkflowStatus.Cancelled:
                // Handle cancellations
                break;
        }
    }
}
catch (OperationCanceledException)
{
    // Handle external cancellation
}
catch (Exception ex)
{
    // Handle unexpected errors
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## 🔄 Best Practices

### 1. **Keep Blocks Focused**
- Each block should have a single responsibility
- Avoid large, complex blocks
- Use composition for complex workflows

### 2. **Use Meaningful Names**
- Use descriptive block IDs and names
- Follow consistent naming conventions
- Document block purposes

### 3. **Handle Errors Gracefully**
- Always specify failure transitions
- Use appropriate guard severity levels
- Log errors with sufficient context

### 4. **Optimize Performance**
- Use parallel execution for independent operations
- Implement proper state cleanup
- Monitor execution times

### 5. **Ensure Testability**
- Make blocks deterministic
- Use dependency injection for external services
- Write comprehensive unit tests

## 📈 Performance Considerations

- **State Size**: Keep workflow state as small as possible
- **Block Granularity**: Balance between too many vs. too few blocks
- **Parallel Execution**: Use for CPU-intensive or I/O operations
- **Persistence**: Choose appropriate storage based on durability needs

## 🔒 Security Considerations

- Validate all inputs in guards
- Use appropriate authorization guards
- Sanitize sensitive data in logs
- Implement proper access controls

## 📋 API Reference

### Core Classes

- **`WorkflowEngine`** - Main workflow execution engine
- **`FlowCoreWorkflowBuilder`** - Fluent API for building workflows
- **`ExecutionContext`** - Context passed between blocks
- **`ExecutionResult`** - Result of block execution
- **`IWorkflowBlock`** - Interface for workflow blocks

### Guard System

- **`IGuard`** - Interface for validation logic
- **`GuardManager`** - Manages guard evaluation
- **`CommonGuards`** - Pre-built guard implementations
- **`GuardResult`** - Result of guard evaluation

### Persistence

- **`IStateManager`** - Interface for state persistence
- **`InMemoryStateManager`** - In-memory state storage
- **`WorkflowStatePersistenceService`** - Automatic state persistence

### Advanced Features

- **`ParallelBlock`** - Execute blocks in parallel
- **`WorkflowBlockBase`** - Base class for custom blocks
- **`WorkflowBlockFactory`** - Creates blocks from definitions

## 🤝 Contributing

To contribute to the workflow engine:

1. Follow the established patterns for new blocks
2. Add comprehensive unit tests
3. Update documentation for new features
4. Ensure backward compatibility

## 📞 Support

For questions, issues, or feature requests:
- Check existing documentation
- Review test examples
- Create issues with detailed descriptions
- Provide minimal reproducible examples

---

**Happy Workflow Development!** 🎉