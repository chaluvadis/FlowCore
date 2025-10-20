# LinkedList Workflow Engine

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)

**A type-safe, predictable workflow orchestration engine for .NET that solves critical limitations in existing workflow solutions.**

## Key Features

- **Type Safety**: Compile-time validation prevents runtime errors
- **Performance**: Skip unnecessary steps based on runtime conditions
- **Reliability**: Built-in validation and error recovery
- **Flexibility**: Runtime adaptability with optional execution
- **Observability**: Complete audit trail and debugging support
- **Composability**: Reusable blocks with clear responsibilities
- **Persistence**: Natural state management for long-running workflows
- **Developer Experience**: Intuitive API with full IntelliSense support

## Table of Contents

- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Usage Examples](#usage-examples)
- [JSON Workflow System](#json-workflow-system)
- [Guard System](#guard-system)
- [State Management](#state-management)
- [Error Handling](#error-handling)
- [Advanced Features](#advanced-features)
- [API Reference](#api-reference)

## Problem Statement

Traditional workflow engines suffer from critical limitations:

### Common Issues

**Type Safety & Debugging**

- Runtime type errors from dynamic inference
- No compile-time validation of workflow definitions
- Limited visibility into execution flow
- Black box approach makes troubleshooting difficult

**Validation & Control**

- No pre-execution validation before moving to next step
- Missing business rule enforcement between workflow steps
- Fixed execution paths with no runtime adaptability
- Every step must execute regardless of necessity

**State Management**

- No native support for long-running tasks (hours/days)
- Complex state management across extended execution periods
- Handling partial failures in multi-step processes

### Real-World Impact

```csharp
// Problems this engine solves:
// Fraud Detection: Always run expensive checks even for trusted customers
// Email Verification: Always send verification for pre-verified users
// Document Processing: Always run OCR even when text is already extracted
// Approval Workflows: Always require manager approval for minor changes
// API Calls: Always make external calls even when data is cached
```

## Quick Start

### Installation

Add the package to your project:

```bash
dotnet add package LinkedListWorkflowEngine.Core
```

### Basic Usage

```csharp
using LinkedListWorkflowEngine.Core;

// Create a simple workflow
var workflow = new WorkflowBuilder("user-registration", "User Registration")
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

### JSON Workflow

```csharp
// Define workflow in JSON
var jsonWorkflow = @"
{
  ""id"": ""order-processing"",
  ""name"": ""Order Processing"",
  ""version"": ""1.0.0"",
  ""startBlockName"": ""validate_order"",
  ""blocks"": {
    ""validate_order"": {
      ""id"": ""validate_order"",
      ""type"": ""BasicBlocks.LogBlock"",
      ""nextBlockOnSuccess"": ""process_payment""
    }
  }
}";

// Execute JSON workflow
var jsonEngine = new JsonWorkflowEngine(serviceProvider);
var result = await jsonEngine.ExecuteFromJsonAsync(jsonWorkflow, orderData);
```

## Core Concepts

### Design Philosophy

**Core Principles:**

1. **Type Safety First**: Compile-time validation over runtime discovery
2. **Predictable Execution**: Clear success/failure paths with guard checks
3. **Optional Execution**: Runtime adaptability with conditional block skipping
4. **Developer Experience**: Feels natural to .NET developers
5. **Composability**: Small, focused blocks over monolithic workflows
6. **State Persistence**: Natural checkpointing at each block boundary
7. **Error Recovery**: Built-in retry and compensation logic
8. **Guard Validation**: Pre/post-execution validation at each step

### Linked-List Architecture

**Core Metaphor:**

```
Workflow = Linked List of Code Blocks

Each block knows:
- What to execute
- What to do on success
- What to do on failure
- When to wait for approval
```

**Key Advantages:**

- **Intuitive**: Every developer understands linked lists
- **Type-Safe**: Compile-time validation prevents runtime errors
- **Transparent**: Clear execution flow, easy to debug
- **Composable**: Blocks can be reused and combined

### Core Architecture Components

#### 1. Workflow Blocks

```csharp
public interface IWorkflowBlock
{
    Task<ExecutionResult> ExecuteAsync(ExecutionContext context);
    string NextBlockOnSuccess { get; }
    string NextBlockOnFailure { get; }
}
```

#### 2. Execution Context

```csharp
public class ExecutionContext
{
    public object Input { get; }
    public IReadOnlyDictionary<string, object> State { get; }
    public CancellationToken CancellationToken { get; }
    public string WorkflowName { get; }
    public DateTime StartedAt { get; }
    public string CurrentBlockName { get; internal set; }
}
```

#### 3. Execution Results

```csharp
public class ExecutionResult
{
    public bool IsSuccess { get; }
    public string NextBlockName { get; }
    public object Output { get; }
    public ExecutionMetadata Metadata { get; }

    // Factory methods for different outcomes
    public static ExecutionResult Success(string nextBlock = null);
    public static ExecutionResult Failure(string nextBlock = null);
    public static ExecutionResult Skip(string nextBlock = null);
    public static ExecutionResult Wait(TimeSpan duration, string nextBlock = null);
}
```

## Key Innovations

### Optional Block Execution

Runtime workflow optimization through intelligent block skipping:

```csharp
public class SmartValidationBlock : IWorkflowBlock
{
    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        // Skip expensive validation for low-risk scenarios
        if (context.Customer.RiskScore == RiskLevel.Low)
            return ExecutionResult.Skip("expensive_validation_block");

        if (context.Order.Amount < 50)
            return ExecutionResult.Skip("payment_validation_block");

        // Only run validation when necessary
        var result = await RunValidation(context);
        return ExecutionResult.Success("payment_processing_block");
    }
}
```

**Execution Strategies:**

- Skip with Alternative: Jump to different block when skipping
- Skip Multiple Blocks: Skip sequences of unnecessary steps
- Conditional Insertion: Add blocks based on runtime conditions
- Dynamic Routing: Change workflow path based on execution results

### Guard System

Pre/post-execution validation at each block boundary:

```csharp
public class SecurePaymentBlock : IWorkflowBlock
{
    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        // Guard Check: Validate before execution
        if (!await ValidatePaymentMethod(context.PaymentInfo))
            return ExecutionResult.Failure("payment_method_invalid");

        if (!await CheckAccountBalance(context.Amount))
            return ExecutionResult.Failure("insufficient_funds");

        if (!await VerifySecurityToken(context.UserToken))
            return ExecutionResult.Failure("security_validation_failed");

        // Only execute payment if all guards pass
        var result = await ProcessPayment(context);
        return ExecutionResult.Success("order_confirmation_block");
    }
}
```

**Guard Types:**

- Data Validation Guards: Type checking and format validation
- Business Rule Guards: Policy and procedure compliance
- System State Guards: Resource availability and security validation

### State Persistence

Natural checkpointing at each block boundary:

```csharp
public class WorkflowEngine
{
    public async Task<WorkflowState> ExecuteAsync(WorkflowDefinition definition, object input)
    {
        var context = new ExecutionContext(input);
        var currentBlock = definition.StartBlock;

        while (currentBlock != null)
        {
            // Execute block and handle result
            var result = await currentBlock.ExecuteAsync(context);

            // Persist state at each checkpoint
            await PersistWorkflowState(definition.Id, context.State);

            // Determine next block based on result
            currentBlock = await GetNextBlock(definition, result.NextBlockName);
        }

        return context.State;
    }
}
```

### Advanced Features

#### Long-Running Workflow Support

- Natural State Persistence: Each block execution is a checkpoint
- Error Recovery: Failed workflows can resume from last successful block
- Progress Tracking: Clear execution history and audit trail
- Resource Management: Memory and connection leak prevention

#### JSON-Based Workflow Definitions

Declarative workflow definitions with runtime interpretation:

```json
{
  "id": "enterprise_security_workflow",
  "version": "2.0.0",
  "description": "Enterprise security workflow with guard checks",
  "metadata": {
    "author": "Security Team",
    "tags": ["security", "enterprise", "validation"]
  },
  "variables": {
    "securityLevel": "high",
    "requireMFA": true,
    "auditLevel": "detailed"
  },
  "nodes": {
    "auth_guard": {
      "type": "MyCompany.SecurityBlocks.AuthenticationGuardBlock, MyCompany.SecurityBlocks",
      "configuration": {
        "tokenValidation": "strict",
        "allowedTokenTypes": ["bearer", "apikey"]
      },
      "guards": {
        "preExecution": [
          {
            "type": "TokenValidationGuard",
            "condition": "token != null && token.IsValid",
            "errorBlock": "invalid_token_error"
          }
        ]
      },
      "transitions": {
        "success": "authz_guard",
        "failure": "auth_failure_handler"
      }
    },
    "authz_guard": {
      "type": "AuthorizationGuardBlock",
      "configuration": {
        "requiredPermission": "admin_access",
        "checkHierarchy": true
      },
      "transitions": {
        "success": "business_validation",
        "failure": "access_denied_handler"
      }
    },
    "business_validation": {
      "type": "BusinessRuleValidationBlock",
      "configuration": {
        "rules": [
          {
            "name": "BusinessHoursCheck",
            "expression": "currentTime >= '09:00' && currentTime <= '17:00'",
            "errorMessage": "Operation outside business hours"
          }
        ]
      },
      "transitions": {
        "success": "execute_business_logic",
        "failure": "business_rule_violation_handler"
      }
    }
  },
  "execution": {
    "startNode": "auth_guard",
    "errorHandler": "global_error_handler",
    "timeout": "00:30:00",
    "retryPolicy": {
      "maxRetries": 3,
      "backoffStrategy": "exponential",
      "initialDelay": "00:00:01"
    }
  },
  "persistence": {
    "provider": "SqlServer",
    "connectionString": "Server=.;Database=WorkflowState;Trusted_Connection=True;",
    "checkpointFrequency": "AfterEachNode"
  }
}
```

**Runtime Interpretation Engine:**

```csharp
public class JsonWorkflowEngine
{
    private readonly IBlockFactory _blockFactory;
    private readonly IGuardEvaluator _guardEvaluator;
    private readonly IStateManager _stateManager;

    public async Task<WorkflowResult> ExecuteFromJson(string jsonDefinition, object input)
    {
        // Parse and validate JSON
        var workflowDefinition = JsonConvert.DeserializeObject<WorkflowDefinition>(jsonDefinition);

        // Create execution context
        var context = new JsonExecutionContext(input, workflowDefinition.Variables);

        // Execute workflow using JSON-defined flow
        return await ExecuteWorkflowAsync(workflowDefinition, context);
    }
}
```

#### Workflow Composition

```csharp
// Complex workflows built by composing focused blocks
var enterpriseWorkflow = new Workflow("enterprise_security_workflow")
    .StartWith(new AuthenticationGuardBlock())
    .Then(new AuthorizationGuardBlock("required_permission"))
    .Then(new BusinessRuleValidationBlock())
    .Then(new DataConsistencyGuardBlock())
    .Then(new SecurityAuditBlock())
    .Then(new ExecuteBusinessLogicBlock())
    .Then(new PostExecutionValidationBlock())
    .OnError(new IntelligentCorrectionBlock("adaptive_failure_handling"));
```

#### Error Handling & Compensation

```csharp
public class CompensatableBlock : IWorkflowBlock
{
    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        var compensationPoint = await CreateCompensationPoint(context);

        try
        {
            var result = await ExecuteBusinessLogic(context);
            return ExecutionResult.Success("next_block");
        }
        catch (Exception ex)
        {
            await CompensateAsync(compensationPoint);
            return ExecutionResult.Failure("error_handling_block");
        }
    }
}
```

## JSON Workflow System

The engine supports declarative workflow definitions through JSON, enabling runtime interpretation and business user control.

### Basic JSON Workflow

```json
{
  "id": "order-processing",
  "name": "Order Processing",
  "version": "1.0.0",
  "description": "Complete order lifecycle with payment processing",
  "startBlockName": "validate_order",
  "blocks": {
    "validate_order": {
      "id": "validate_order",
      "type": "BasicBlocks.LogBlock",
      "nextBlockOnSuccess": "process_payment",
      "nextBlockOnFailure": "reject_order"
    },
    "process_payment": {
      "id": "process_payment",
      "type": "BasicBlocks.LogBlock",
      "nextBlockOnSuccess": "send_confirmation"
    },
    "send_confirmation": {
      "id": "send_confirmation",
      "type": "BasicBlocks.LogBlock"
    },
    "reject_order": {
      "id": "reject_order",
      "type": "BasicBlocks.LogBlock"
    }
  },
  "variables": {
    "minOrderAmount": 10.0,
    "maxOrderAmount": 10000.0
  }
}
```

### JSON Execution

```csharp
var jsonEngine = new JsonWorkflowEngine(serviceProvider);
var jsonDefinition = File.ReadAllText("workflow-definition.json");
var orderData = new { OrderId = "ORD-001", Amount = 299.99m };

var result = await jsonEngine.ExecuteFromJsonAsync(jsonDefinition, orderData);
```

### Advanced JSON Features

#### Guard Definitions

```json
{
  "guards": {
    "preExecution": [
      {
        "type": "BusinessRuleGuard",
        "condition": "order.Amount >= variables.minOrderAmount",
        "errorBlock": "amount_too_low"
      }
    ]
  }
}
```

#### Conditional Transitions

```json
{
  "transitions": {
    "success": {
      "condition": "result.Amount > 1000",
      "target": "high_value_processing"
    },
    "default": "standard_processing"
  }
}
```

#### Dynamic Configuration

```json
{
  "configuration": {
    "endpoint": "{{variables.apiBaseUrl}}/process",
    "timeout": "{{variables.requestTimeout}}",
    "headers": {
      "Authorization": "Bearer {{context.user.token}}"
    }
  }
}
```

### JSON Workflow Benefits

- Declarative: Define workflows without code compilation
- Business Control: Enable business user workflow modification
- Runtime Updates: Support hot-reloading of workflow definitions
- External Storage: Store definitions in databases or files
- A/B Testing: Test different workflow versions simultaneously
- Analytics: Monitor workflow performance and execution patterns

## Usage Examples

### Basic Workflow

```csharp
var workflow = new WorkflowBuilder("user-registration", "User Registration")
    .WithVersion("1.0.0")
    .WithDescription("Simple user registration process")
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

var engine = new WorkflowEngine(blockFactory);
var input = new { Username = "john_doe", Email = "john@example.com" };
var result = await engine.ExecuteAsync(workflow, input);
```

### E-commerce Order Processing

```csharp
var workflow = new WorkflowBuilder("order-processing", "Order Processing")
    .WithVersion("2.0.0")
    .WithVariable("minOrderAmount", 10.0)
    .WithVariable("maxOrderAmount", 10000.0)
    .StartWith("BasicBlocks.LogBlock", "validate_order")
        .OnSuccessGoTo("process_payment")
        .OnFailureGoTo("reject_order")
        .WithDisplayName("Validate Order Details")
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
        .WithDisplayName("Payment Failed")
        .And()
    .Build();

var orderData = new
{
    OrderId = "ORD-2024-001",
    CustomerId = "CUST-001",
    Amount = 299.99m,
    Items = new[] { "Laptop", "Mouse" }
};

var result = await engine.ExecuteAsync(workflow, orderData);
```

### Customer Onboarding with Parallel Processing

```csharp
var workflow = new WorkflowBuilder("customer-onboarding", "Customer Onboarding")
    .WithVersion("1.0.0")
    .WithVariable("welcomeEmailTemplate", "Welcome to our platform!")
    .StartWith("BasicBlocks.LogBlock", "initialize_onboarding")
        .OnSuccessGoTo("parallel_setup")
        .WithDisplayName("Initialize Customer Onboarding")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "parallel_setup")
        .OnSuccessGoTo("create_profile")
        .WithDisplayName("Parallel Customer Setup")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "create_profile")
        .OnSuccessGoTo("send_welcome")
        .WithDisplayName("Create Customer Profile")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "send_welcome")
        .OnSuccessGoTo("schedule_followup")
        .WithDisplayName("Send Welcome Package")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "schedule_followup")
        .WithDisplayName("Schedule Follow-up Call")
        .And()
    .Build();

var customerData = new
{
    CustomerId = "CUST-NEW-001",
    CompanyName = "TechCorp Solutions",
    PrimaryContact = new { Name = "Sarah Johnson", Email = "sarah@techcorp.com" }
};

var result = await engine.ExecuteAsync(workflow, customerData);
```

### Document Processing Pipeline

```csharp
var workflow = new WorkflowBuilder("document-processing", "Document Processing")
    .WithVersion("3.0.0")
    .WithVariable("maxFileSize", 10485760L) // 10MB
    .WithVariable("supportedFormats", new[] { "PDF", "JPG", "PNG", "TIFF" })
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
    .Build();

var documentData = new
{
    DocumentId = "DOC-2024-001",
    FileName = "invoice_techcorp_001.pdf",
    FileSize = 2048576L, // 2MB
    FileFormat = "PDF"
};

var result = await engine.ExecuteAsync(workflow, documentData);
```

### Guard Validation Examples

```csharp
// Business Hours Guard
var businessHoursGuard = new CommonGuards.BusinessHoursGuard(
    TimeSpan.FromHours(9), TimeSpan.FromHours(17));

// Data Format Guard
var emailGuard = new CommonGuards.DataFormatGuard("Email", @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

// Numeric Range Guard
var amountGuard = new CommonGuards.NumericRangeGuard("Amount", 100.0m, 10000.0m);

// Required Field Guard
var requiredGuard = new CommonGuards.RequiredFieldGuard("CustomerId", "Email", "Amount");

// Authorization Guard
var authGuard = new CommonGuards.AuthorizationGuard("admin", "administrator", "manager");
```

### Adaptive Workflow with Optional Blocks

```csharp
var intelligentWorkflow = new Workflow("adaptive_order_processing")
    .StartWith(new CustomerAnalysisBlock())
    .Then(new OptionalFraudCheckBlock())              // Skip if low risk
    .Then(new OptionalAddressValidationBlock())       // Skip if verified
    .Then(new OptionalCreditCheckBlock())             // Skip if pre-approved
    .Then(new PaymentProcessingBlock())
    .Then(new OptionalEnhancedShippingBlock())        // Skip for standard orders
    .Then(new OptionalPremiumSupportBlock());         // Skip for basic customers
```

### Production-Ready Execution

```csharp
var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
var engine = new WorkflowEngine(blockFactory, logger: logger);

var realisticInput = new
{
    CustomerId = "CUST-PREMIUM-001",
    OrderId = "ORD-2024-001",
    Amount = 1250.50m,
    Items = new[] { "Premium Laptop", "Wireless Mouse", "USB-C Hub" },
    ShippingAddress = "123 Business Ave, Tech City, TC 12345",
    PaymentMethod = "CreditCard",
    CustomerTier = "Premium",
    ProcessingPriority = "High"
};

var result = await engine.ExecuteAsync(workflowDefinition, realisticInput);
```

### **JSON-Defined Order Processing Workflow**

```json
{
  "id": "intelligent_order_processing",
  "version": "2.0.0",
  "description": "Adaptive order processing with optional validations",
  "variables": {
    "lowRiskThreshold": 100,
    "highValueThreshold": 10000,
    "trustedCustomerScore": 0.8
  },
  "nodes": {
    "customer_analysis": {
      "type": "CustomerRiskAssessmentBlock",
      "configuration": {
        "riskModel": "ML_v2.0",
        "factors": ["orderHistory", "paymentHistory", "accountAge"]
      },
      "transitions": {
        "success": "fraud_check"
      }
    },
    "fraud_check": {
      "type": "OptionalFraudCheckBlock",
      "configuration": {
        "skipConditions": [
          "context.Customer.RiskScore < variables.trustedCustomerScore",
          "context.Order.Amount < variables.lowRiskThreshold"
        ]
      },
      "transitions": {
        "success": "address_validation",
        "skip": "payment_processing"
      }
    },
    "address_validation": {
      "type": "OptionalAddressValidationBlock",
      "configuration": {
        "skipIfVerified": true,
        "verificationService": "AddressService_v2"
      },
      "transitions": {
        "success": "credit_check",
        "skip": "payment_processing"
      }
    },
    "credit_check": {
      "type": "OptionalCreditCheckBlock",
      "configuration": {
        "skipForPreApproved": true,
        "creditBureau": "Experian"
      },
      "transitions": {
        "success": "payment_processing",
        "skip": "payment_processing"
      }
    },
    "payment_processing": {
      "type": "AdaptivePaymentProcessingBlock",
      "configuration": {
        "providers": ["Stripe", "PayPal", "BankTransfer"],
        "routingRules": {
          "amount < 100": "PayPal",
          "amount >= 10000": "BankTransfer",
          "default": "Stripe"
        }
      },
      "transitions": {
        "success": "shipping_decision"
      }
    },
    "shipping_decision": {
      "type": "DecisionBlock",
      "configuration": {
        "conditions": [
          {
            "expression": "order.Amount >= variables.highValueThreshold",
            "target": "enhanced_shipping"
          },
          {
            "expression": "order.IsInternational",
            "target": "international_shipping"
          }
        ],
        "defaultTarget": "standard_shipping"
      }
    },
    "standard_shipping": {
      "type": "ShippingBlock",
      "configuration": {
        "carrier": "UPS_Ground",
        "signatureRequired": false
      },
      "transitions": {
        "success": "completion_notification"
      }
    },
    "enhanced_shipping": {
      "type": "ShippingBlock",
      "configuration": {
        "carrier": "FedEx_Express",
        "signatureRequired": true,
        "insurance": "full_value"
      },
      "transitions": {
        "success": "completion_notification"
      }
    },
    "international_shipping": {
      "type": "ShippingBlock",
      "configuration": {
        "carrier": "DHL_Express",
        "customsDeclaration": true,
        "tracking": "detailed"
      },
      "transitions": {
        "success": "completion_notification"
      }
    },
    "completion_notification": {
      "type": "MultiChannelNotificationBlock",
      "configuration": {
        "channels": ["Email", "SMS"],
        "template": "order_shipped",
        "personalization": {
          "customerName": "{{context.Customer.Name}}",
          "orderNumber": "{{context.Order.Id}}",
          "trackingNumber": "{{result.TrackingNumber}}"
        }
      },
      "transitions": {
        "success": "workflow_complete"
      }
    },
    "workflow_complete": {
      "type": "WorkflowCompletionBlock",
      "configuration": {
        "archiveData": true,
        "triggerAnalytics": true
      }
    }
  },
  "execution": {
    "startNode": "customer_analysis",
    "timeout": "00:10:00",
    "retryPolicy": {
      "maxRetries": 2,
      "backoffStrategy": "linear"
    }
  },
  "persistence": {
    "provider": "SqlServer",
    "checkpointFrequency": "AfterEachNode"
  }
}
```

### **Runtime JSON Workflow Execution**

```csharp
public class WorkflowService
{
    private readonly JsonWorkflowEngine _engine;

    public async Task<WorkflowResult> ExecuteOrderWorkflow(string customerId, OrderData order)
    {
        // Load workflow definition from database or file
        var jsonDefinition = await _workflowRepository.GetWorkflowDefinition("intelligent_order_processing");

        // Prepare input context
        var input = new
        {
            CustomerId = customerId,
            Order = order,
            Timestamp = DateTime.UtcNow
        };

        // Execute workflow from JSON definition
        return await _engine.ExecuteFromJson(jsonDefinition, input);
    }

    public async Task<WorkflowResult> ExecuteWorkflowById(string workflowId, object input)
    {
        var definition = await _workflowRepository.GetWorkflowDefinition(workflowId);
        return await _engine.ExecuteFromJson(definition, input);
    }
}
```

## Guard System

The framework includes a sophisticated guard system for pre/post-execution validation:

### Built-in Guards

#### Business Hours Guard

```csharp
var businessHoursGuard = new CommonGuards.BusinessHoursGuard(
    TimeSpan.FromHours(9), TimeSpan.FromHours(17));
```

#### Data Format Guard

```csharp
var emailGuard = new CommonGuards.DataFormatGuard(
    "Email", @"^[^\s@]+@[^\s@]+\.[^\s@]+$");
```

#### Numeric Range Guard

```csharp
var amountGuard = new CommonGuards.NumericRangeGuard(
    "Amount", 10.0m, 5000.0m);
```

#### Required Field Guard

```csharp
var requiredGuard = new CommonGuards.RequiredFieldGuard(
    "CustomerId", "Email", "Amount");
```

#### Authorization Guard

```csharp
var authGuard = new CommonGuards.AuthorizationGuard(
    "admin", "administrator", "manager");
```

### Usage in Workflows

```csharp
var workflow = new WorkflowBuilder("guarded-workflow", "Guarded Workflow")
    .StartWith("BasicBlocks.LogBlock", "validate_request")
        .OnSuccessGoTo("process_request")
        .OnFailureGoTo("validation_failed")
        .WithDisplayName("Validate Request with Guards")
        .And()
    .AddBlock("BasicBlocks.LogBlock", "process_request")
        .OnSuccessGoTo("complete")
        .WithDisplayName("Process Valid Request")
        .And()
    .Build();
```

## State Management

### Automatic Checkpointing

The engine automatically persists state at each block boundary:

```csharp
var engine = new WorkflowEngine(
    blockFactory,
    stateManager: new InMemoryStateManager(),
    stateManagerConfig: new StateManagerConfig
    {
        CheckpointFrequency = CheckpointFrequency.AfterEachBlock
    });
```

### Long-Running Workflow Support

```csharp
// Workflows can be suspended and resumed
await engine.SuspendWorkflowAsync(workflowId, executionId, context);

// Resume from checkpoint
var result = await engine.ResumeFromCheckpointAsync(
    workflowDefinition, executionId);
```

## Error Handling

### Built-in Error Recovery

```csharp
var workflow = new WorkflowBuilder("resilient-workflow", "Resilient Workflow")
    .StartWith("BasicBlocks.LogBlock", "process_critical_task")
        .OnSuccessGoTo("continue_workflow")
        .OnFailureGoTo("handle_error") // Custom error handling
        .And()
    .AddBlock("BasicBlocks.LogBlock", "handle_error")
        .OnSuccessGoTo("retry_task")
        .WithDisplayName("Handle Error and Retry")
        .And()
    .Build();
```

### Retry Policies

```csharp
var executionConfig = new WorkflowExecutionConfig
{
    RetryPolicy = new RetryPolicy
    {
        MaxRetries = 3,
        BackoffStrategy = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(1)
    }
};
```

## Advanced Features

### Parallel Execution

```csharp
var parallelWorkflow = new Workflow("parallel_processing")
    .StartWith(new ParallelBlock(new[]
    {
        new ValidationBlock("validation_1"),
        new ValidationBlock("validation_2"),
        new ValidationBlock("validation_3")
    }))
    .Then(new AggregationBlock());
```

### Conditional Execution

```csharp
var conditionalWorkflow = new Workflow("conditional_processing")
    .StartWith(new DecisionBlock(ctx =>
        ctx.Input.Amount > 1000 ? "premium_path" : "standard_path"))
    .Then(new PremiumProcessingBlock()) // Only for high-value orders
    .Then(new StandardProcessingBlock()); // Default path
```

### Dynamic Workflow Modification

```csharp
// Modify workflow at runtime based on conditions
var dynamicWorkflow = new Workflow("dynamic_processing")
    .StartWith(new AdaptiveBlock(ctx =>
    {
        // Add blocks dynamically based on context
        if (ctx.Input.RequiresSpecialHandling)
        {
            return ExecutionResult.Success("special_handling_block");
        }
        return ExecutionResult.Success("standard_processing_block");
    }));
```

### Real-Time Adaptation

```csharp
var currentTime = DateTime.UtcNow;
var isPeakHour = currentTime.Hour >= 9 && currentTime.Hour <= 17;

var adaptiveWorkflow = new WorkflowBuilder("adaptive-workflow", "Adaptive Processing")
    .WithVariable("currentTime", currentTime)
    .WithVariable("isPeakHour", isPeakHour)
    .WithVariable("processingPriority", isPeakHour ? "Expedited" : "Standard")
    .Build();
```

## API Reference

### Core Classes

- **`WorkflowEngine`**: Main workflow execution engine
- **`WorkflowBuilder`**: Fluent API for building workflows
- **`JsonWorkflowEngine`**: JSON-based workflow execution
- **`ExecutionContext`**: Runtime execution context
- **`ExecutionResult`**: Block execution outcomes

### Built-in Blocks

- **`BasicBlocks.LogBlock`**: Logging and debugging
- **`BasicBlocks.WaitBlock`**: Delay execution
- **`BasicBlocks.SetStateBlock`**: Modify workflow state
- **`BasicBlocks.ConditionalBlock`**: Conditional logic
- **`BasicBlocks.FailBlock`**: Force failure scenarios

### Guard Classes

- **`CommonGuards.BusinessHoursGuard`**: Time-based validation
- **`CommonGuards.DataFormatGuard`**: Format validation
- **`CommonGuards.NumericRangeGuard`**: Range validation
- **`CommonGuards.RequiredFieldGuard`**: Required field validation
- **`CommonGuards.AuthorizationGuard`**: Permission validation

### Extension Points

- **`IWorkflowBlock`**: Custom block implementation
- **`IWorkflowBlockFactory`**: Block creation and resolution
- **`IStateManager`**: State persistence abstraction
- **`IGuard`**: Custom guard implementation

---

## Key Benefits

### Core Workflow Engine Benefits

- Type Safety: Compile-time validation prevents runtime errors
- Performance: Skip unnecessary steps based on runtime conditions
- Reliability: Built-in validation and error recovery
- Flexibility: Runtime adaptability with optional execution
- Observability: Complete audit trail and debugging support
- Maintainability: Composable blocks with clear responsibilities
- Scalability: Natural state persistence for long-running workflows
- Developer Experience: Intuitive API with full IntelliSense support

### JSON Workflow System Benefits

- Declarative Definition: Define workflows without code compilation
- Runtime Modification: Update workflows without application restart
- Business User Control: Enable non-developers to modify workflows
- External Storage: Store workflow definitions in databases or files
- Version Management: Track and manage workflow definition versions
- A/B Testing: Test different workflow versions simultaneously
- Analytics: Monitor workflow performance and execution patterns
- Integration: Easy integration with external systems and tools

---

## Security and Reliability

### Enhanced Security Features

The LinkedListWorkflowEngine now includes comprehensive security hardening to protect against dynamic assembly loading vulnerabilities:

#### Key Security Improvements

- **Dynamic Assembly Loading Protection**: Assembly loading is disabled by default and requires explicit opt-in
- **Assembly Whitelist Support**: Only explicitly allowed assemblies can be loaded dynamically
- **Strong-Name Signature Validation**: All assemblies must have valid strong-name signatures
- **Runtime Security Validation**: Real-time validation of assembly security properties
- **Comprehensive Audit Trail**: All security events are logged for compliance and monitoring

#### Secure Configuration

```csharp
// Secure by default - dynamic loading disabled
var factory = new WorkflowBlockFactory(
    serviceProvider: new ServiceCollection().BuildServiceProvider(),
    securityOptions: new WorkflowBlockFactorySecurityOptions(),
    logger: null);

// Explicit security configuration for trusted scenarios
var securityOptions = new WorkflowBlockFactorySecurityOptions
{
    AllowDynamicAssemblyLoading = true,
    AllowedAssemblyNames = new[] { "MyCompany.TrustedBlocks" },
    ValidateStrongNameSignatures = true,
    AllowedPublicKeyTokens = new[] { yourCompanyPublicKeyToken }
};

var secureFactory = new WorkflowBlockFactory(
    serviceProvider: new ServiceCollection().BuildServiceProvider(),
    securityOptions: securityOptions,
    logger: null);
```

#### Security Documentation

For comprehensive security guidelines, best practices, and safe extension patterns, see [SECURITY.md](SECURITY.md).

---

## **Comprehensive Example Suite**

### **Production-Ready Demonstrations**

The framework now includes 7 comprehensive examples that demonstrate real-world usage patterns:

#### **1. Real-Time User Registration**

- **Dynamic Context**: Peak hour detection and adaptive processing
- **Realistic Data**: Complete user profiles with timestamps and device info
- **Adaptive Logic**: Different processing based on registration volume
- **Business Variables**: Configurable delays and priority assignment

#### **2. E-commerce Order Processing**

- **Complete Lifecycle**: From validation to fulfillment and notification
- **Business Data**: Orders, customers, payments, shipping, inventory
- **Error Scenarios**: Payment failures, inventory issues, shipping problems
- **Stakeholder Integration**: Notifications, confirmations, status updates

#### **3. Customer Onboarding Workflow**

- **Parallel Processing**: Multiple setup tasks running simultaneously
- **Enterprise Context**: Company information, industry classification
- **Stakeholder Management**: Follow-up scheduling and notifications
- **Business Logic**: Subscription tiers, priorities, assigned managers

#### **4. Document Processing Pipeline**

- **Technical Implementation**: OCR, classification, storage, thumbnails
- **File Management**: Size limits, format validation, metadata generation
- **Department Integration**: Cross-team workflows and processing
- **Quality Assurance**: Content validation and SEO optimization

#### **5. Product Catalog Management**

- **E-commerce Focus**: Product information, pricing, inventory management
- **Content Processing**: Image handling, SEO generation, review workflows
- **Publishing Logic**: Auto-publish vs manual review based on business rules
- **Stakeholder Communication**: Catalog updates and notifications

#### **6. Payment Processing with Recovery**

- **Financial Systems**: Multiple payment gateways and retry mechanisms
- **Risk Management**: Circuit breakers, fallback options, authorization
- **Recovery Patterns**: Intelligent retry with exponential backoff
- **Compliance**: Audit trails, confirmation systems, webhook processing

#### **7. Comprehensive Guard Validation**

- **Real Guard Usage**: Actual guard class instantiation and evaluation
- **Business Rule Validation**: Time-based, format, range, authorization checks
- **Integration Patterns**: Guards affecting workflow execution paths
- **Enterprise Scenarios**: Multi-guard validation with complex business rules

### **Educational Enhancements**

#### **Real-Time Context Awareness**

- **Dynamic Variables**: Runtime condition evaluation and assignment
- **Adaptive Processing**: Different execution paths based on current state
- **Intelligent Timing**: Conditional delays and processing priorities
- **Load Balancing**: Automatic resource allocation based on demand

#### **Production Data Patterns**

- **Realistic Identifiers**: Meaningful IDs like "ORD-2024-001", "CUST-PREMIUM-001"
- **Business Context**: Industry terminology, department structures, realistic workflows
- **Complete Data Models**: Full object models representing real business entities
- **Error Scenarios**: Practical failure modes with appropriate recovery strategies

#### **Enterprise Integration**

- **Multi-System Coordination**: Integration between different business systems
- **Stakeholder Communication**: Notifications, approvals, status updates
- **Audit Compliance**: Complete tracking and logging for regulatory requirements
- **Performance Optimization**: Load balancing, caching, and resource management

---

## Core Innovation

### Linked-List Workflow Architecture

**Type-Safe Workflow Definitions:**

- No more runtime type inference errors
- Compile-time validation of workflow structure
- IntelliSense support for workflow creation
- Clear execution flow visualization

### JSON-Based Workflow System

**Declarative Workflow Definitions:**

- Complete workflow definition in JSON format
- Runtime interpretation and execution
- Dynamic workflow loading and modification
- Separation of workflow logic from orchestration

**Key Innovation Matrix:**

| Feature      | Code-Based           | JSON-Based            | Benefit       |
| ------------ | -------------------- | --------------------- | ------------- |
| Definition   | Compile-time         | Runtime               | Flexibility   |
| Modification | Recompile required   | Hot-reload            | Agility       |
| Storage      | Embedded code        | External files/DB     | Manageability |
| Versioning   | Code versioning      | Definition versioning | Control       |
| Testing      | Code testing         | Definition testing    | Separation    |
| Analytics    | Code instrumentation | Execution tracking    | Insights      |

### Hybrid Execution Model

The framework supports both programming paradigms:

**Code-First Approach:**

```csharp
var workflow = new Workflow("process")
    .StartWith(new ValidationBlock())
    .Then(new ProcessingBlock())
    .Then(new NotificationBlock());
```

**JSON-First Approach:**

```csharp
var engine = new JsonWorkflowEngine();
var result = await engine.ExecuteFromJson(jsonDefinition, inputData);
```

**Hybrid Approach:**

```csharp
// Load JSON workflow and extend with code blocks
var jsonWorkflow = await LoadWorkflowFromJson("base_process");
var extendedWorkflow = jsonWorkflow
    .Then(new CustomPostProcessingBlock())
    .Then(new CustomNotificationBlock());
```

This linked-list approach provides type-safe, predictable workflow orchestration that feels natural to .NET developers while solving the critical limitations of existing workflow solutions. The addition of JSON-based workflow definitions extends this capability to support declarative, runtime-modifiable workflows that enable true business user control and operational agility.
