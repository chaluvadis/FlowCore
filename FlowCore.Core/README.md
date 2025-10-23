# FlowCore

A type-safe, predictable workflow orchestration engine for .NET that solves critical limitations in existing workflow solutions.

## Key Features

- **Type Safety**: Compile-time validation prevents runtime errors
- **Performance**: Skip unnecessary steps based on runtime conditions
- **Reliability**: Built-in validation and error recovery
- **Flexibility**: Runtime adaptability with optional execution
- **Observability**: Complete audit trail and debugging support
- **Composability**: Reusable blocks with clear responsibilities
- **Persistence**: Natural state management for long-running workflows
- **Developer Experience**: Intuitive API with full IntelliSense support

## Quick Start

```csharp
using FlowCore.Core;

// Create a simple workflow using the fluent API
var workflow = FlowCoreWorkflowBuilder.Create("user-registration", "User Registration")
    .WithVersion("1.0.0")
    .WithDescription("User registration process")
    .StartWith("BasicBlocks.LogBlock", "validate_input")
        .OnSuccessGoTo("create_user")
        .WithDisplayName("Validate User Input")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "create_user")
        .OnSuccessGoTo("send_email")
        .WithDisplayName("Create User Account")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "send_email")
        .WithDisplayName("Send Welcome Email")
        .And()
    .Build();

// Execute the workflow
var engine = new WorkflowEngine(blockFactory);
var input = new { Username = "john_doe", Email = "john@example.com" };
var result = await engine.ExecuteAsync(workflow, input);

Console.WriteLine($"Workflow completed: {result.Succeeded}");
```

## Installation

```bash
dotnet add package FlowCore
```

## Dependency Injection Setup

FlowCore integrates seamlessly with Microsoft.Extensions.DependencyInjection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using FlowCore.Core;

// Configure services
var services = new ServiceCollection();

// Register FlowCore services
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddConsole();
});

// Register workflow block factory (implement IWorkflowBlockFactory)
services.AddSingleton<IWorkflowBlockFactory, YourWorkflowBlockFactory>();

// Register state manager (optional, defaults to InMemoryStateManager)
services.AddSingleton<IStateManager, InMemoryStateManager>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Use in your application
var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
var stateManager = serviceProvider.GetRequiredService<IStateManager>();

var engine = new WorkflowEngine(blockFactory, stateManager: stateManager, logger: logger);
```

## Usage Example

```csharp
// Create workflow using fluent API
var workflow = FlowCoreWorkflowBuilder.Create("user-registration", "User Registration")
    .WithVersion("1.0.0")
    .WithDescription("User registration process")
    .WithVariable("welcomeEmailTemplate", "Welcome to our platform!")
    .StartWith("BasicBlocks.LogBlock", "validate_input")
        .OnSuccessGoTo("create_user")
        .WithDisplayName("Validate User Input")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "create_user")
        .OnSuccessGoTo("send_email")
        .WithDisplayName("Create User Account")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "send_email")
        .WithDisplayName("Send Welcome Email")
        .And()
    .Build();

// Execute workflow
var input = new
{
    Username = "john_doe",
    Email = "john@example.com",
    FirstName = "John",
    LastName = "Doe"
};

var result = await engine.ExecuteAsync(workflow, input);

Console.WriteLine($"Workflow completed: {result.Succeeded}");
Console.WriteLine($"Duration: {result.Duration?.TotalMilliseconds}ms");
```

## Documentation

For comprehensive documentation, examples, and API reference, visit the [main repository](https://github.com/your-repo/FlowCore).