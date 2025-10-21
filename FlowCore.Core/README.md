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

## Documentation

For comprehensive documentation, examples, and API reference, visit the [main repository](https://github.com/your-repo/FlowCore).