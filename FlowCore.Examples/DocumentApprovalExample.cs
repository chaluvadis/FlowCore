namespace FlowCore.Examples;

/// <summary>
/// Interface for setting initial status.
/// </summary>
public interface ISetInitialStatusBlock : IWorkflowBlock
{
    Task<ExecutionResult> SetInitialStatusAsync(Models.ExecutionContext context);
}

/// <summary>
/// Custom block for setting initial status.
/// </summary>
public class SetInitialStatusBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase(), ISetInitialStatusBlock
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Set initial status
        context.SetState("Status", "Being Entered");
        LogInfo("Initial status set to 'Being Entered'");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }

    public async Task<ExecutionResult> SetInitialStatusAsync(Models.ExecutionContext context) => await ExecuteBlockAsync(context);
}

/// <summary>
/// Interface for creating a document.
/// </summary>
public interface ICreateDocumentBlock : IWorkflowBlock
{
    Task<ExecutionResult> CreateDocumentAsync(Models.ExecutionContext context);
}

/// <summary>
/// Custom block for creating a document.
/// </summary>
public class CreateDocumentBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase(), ICreateDocumentBlock
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Create document logic
        context.SetState("DocumentId", context.Input.GetType().GetProperty("DocumentId")?.GetValue(context.Input)?.ToString() ?? "Unknown");
        LogInfo("Document created");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }

    public async Task<ExecutionResult> CreateDocumentAsync(Models.ExecutionContext context)
        => await ExecuteBlockAsync(context);
}

/// <summary>
/// Interface for setting status.
/// </summary>
public interface ISetStatusBlock : IWorkflowBlock
{
    Task<ExecutionResult> SetStatusAsync(Models.ExecutionContext context, string status);
}

/// <summary>
/// Custom block for setting status.
/// </summary>
public class SetStatusBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase(), ISetStatusBlock
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // In a real implementation, access configuration like: var status = Configuration["status"]?.ToString() ?? "Unknown";
        // For this example, using hardcoded value
        var status = "Being Entered";
        context.SetState("Status", status);
        LogInfo($"Status set to '{status}'");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }

    public async Task<ExecutionResult> SetStatusAsync(Models.ExecutionContext context, string status)
    {
        context.SetState("Status", status);
        LogInfo($"Status set to '{status}'");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for validating a document.
/// </summary>
public class ValidateDocumentBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Validate document logic
        LogInfo("Document validated");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for sending for approval.
/// </summary>
public class SendForApprovalBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Send for approval logic
        LogInfo("Document sent for approval");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for awaiting approval.
/// </summary>
public class AwaitApprovalBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Await approval logic
        LogInfo("Awaiting approval");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for checking approval.
/// </summary>
public class CheckApprovalBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Check approval logic
        LogInfo("Approval checked");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for routing to department.
/// </summary>
public class RouteToDepartmentBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Route to department logic
        LogInfo("Document routed to department");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for setting access.
/// </summary>
public class SetAccessBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // In a real implementation, access configuration like: var visibleTo = Configuration["visibleTo"] as string[] ?? new string[0];
        // For this example, using hardcoded value
        var visibleTo = new[] { "creator", "manager", "department" };
        context.SetState("VisibleTo", visibleTo);
        LogInfo($"Access set to: {string.Join(", ", visibleTo)}");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for approval complete.
/// </summary>
public class ApprovalCompleteBlock : WorkflowBlockBase
{
    public override string NextBlockOnSuccess { get; protected set; } = "";
    public override string NextBlockOnFailure { get; protected set; } = "";

    public ApprovalCompleteBlock()
        : base()
    {
    }

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Approval complete logic
        LogInfo("Approval complete");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for sending back for updates.
/// </summary>
public class SendBackForUpdatesBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Send back for updates logic
        LogInfo("Document sent back for updates");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for updating document.
/// </summary>
public class UpdateDocumentBlock(string nextBlockOnSuccess, string nextBlockOnFailure = "") : WorkflowBlockBase()
{
    public override string NextBlockOnSuccess { get; protected set; } = nextBlockOnSuccess;
    public override string NextBlockOnFailure { get; protected set; } = nextBlockOnFailure;

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Update document logic
        LogInfo("Document updated");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for validation failed.
/// </summary>
public class ValidationFailedBlock : WorkflowBlockBase
{
    public override string NextBlockOnSuccess { get; protected set; } = "";
    public override string NextBlockOnFailure { get; protected set; } = "";

    public ValidationFailedBlock()
        : base()
    {
    }

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Validation failed logic
        LogInfo("Validation failed");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Custom block for approval rejected.
/// </summary>
public class ApprovalRejectedBlock : WorkflowBlockBase
{
    public override string NextBlockOnSuccess { get; protected set; } = "";
    public override string NextBlockOnFailure { get; protected set; } = "";

    public ApprovalRejectedBlock()
        : base()
    {
    }

    protected override async Task<ExecutionResult> ExecuteBlockAsync(Models.ExecutionContext context)
    {
        // Placeholder: Approval rejected logic
        LogInfo("Approval rejected");
        return ExecutionResult.Success(NextBlockOnSuccess);
    }
}

/// <summary>
/// Example demonstrating a document approval workflow using FlowCore.
/// Handles document creation, approval, and routing with access controls.
/// </summary>
public static class DocumentApprovalExample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== FlowCore Document Approval Workflow Example ===");
        Console.WriteLine("Demonstrating: Document creation, approval process, and access controls");
        Console.WriteLine();

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        services.AddSingleton(sp =>
        {
            return new WorkflowBlockFactorySecurityOptions
            {
                AllowDynamicAssemblyLoading = true,
                AllowedAssemblyNames = ["FlowCore"]
            };
        });
        services.AddSingleton(sp =>
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

        // Create custom blocks with placeholder logic
        // Note: The remaining blocks follow the same pattern as above, implementing interfaces for abstraction.
        // For example, ValidateDocumentBlock implements IValidateDocumentBlock, etc.
        var setInitialStatusBlock = new SetInitialStatusBlock("create_document");
        var createDocumentBlock = new CreateDocumentBlock("set_status_entered");
        var setStatusEnteredBlock = new SetStatusBlock("validate_document");
        var validateDocumentBlock = new ValidateDocumentBlock("send_for_approval", "validation_failed");
        var sendForApprovalBlock = new SendForApprovalBlock("await_approval");
        var awaitApprovalBlock = new AwaitApprovalBlock("check_approval", "approval_rejected");
        var checkApprovalBlock = new CheckApprovalBlock("route_to_department", "send_back_for_updates");
        var routeToDepartmentBlock = new RouteToDepartmentBlock("set_access");
        var setAccessBlock = new SetAccessBlock("approval_complete");
        var approvalCompleteBlock = new ApprovalCompleteBlock();
        var sendBackForUpdatesBlock = new SendBackForUpdatesBlock("update_document");
        var updateDocumentBlock = new UpdateDocumentBlock("send_for_approval");
        var validationFailedBlock = new ValidationFailedBlock();
        var approvalRejectedBlock = new ApprovalRejectedBlock();

        // Create guards
        var approvalGuard = GuardDefinition.CreatePreExecution("auth_guard", "CommonGuards.AuthorizationGuard", "FlowCore", new Dictionary<string, object> { ["requiredPermission"] = "approve_document", ["allowedRoles"] = new[] { "manager" } });

        // Create workflow definition manually with realistic parameters
        var blocks = new Dictionary<string, WorkflowBlockDefinition>
        {
            ["set_initial_status"] = WorkflowBlockDefinition.Create("set_initial_status", "SetInitialStatusBlock", "FlowCore", "create_document", "", new Dictionary<string, object> { ["initialStatus"] = "Being Entered", ["createdBy"] = "system" }),
            ["create_document"] = WorkflowBlockDefinition.Create("create_document", "CreateDocumentBlock", "FlowCore", "set_status_entered", "", new Dictionary<string, object> { ["documentType"] = "invoice", ["maxSize"] = 10485760L }),
            ["set_status_entered"] = WorkflowBlockDefinition.Create("set_status_entered", "SetStatusBlock", "FlowCore", "validate_document", "", new Dictionary<string, object> { ["status"] = "Being Entered", ["timestamp"] = DateTime.UtcNow }),
            ["validate_document"] = WorkflowBlockDefinition.Create("validate_document", "ValidateDocumentBlock", "FlowCore", "send_for_approval", "validation_failed", new Dictionary<string, object> { ["requiredFields"] = new[] { "title", "content", "createdBy" }, ["maxContentLength"] = 10000 }),
            ["send_for_approval"] = WorkflowBlockDefinition.Create("send_for_approval", "SendForApprovalBlock", "FlowCore", "await_approval", "", new Dictionary<string, object> { ["approvalType"] = "manager", ["priority"] = "normal" }),
            ["await_approval"] = WorkflowBlockDefinition.Create("await_approval", "AwaitApprovalBlock", "FlowCore", "check_approval", "approval_rejected", new Dictionary<string, object> { ["timeout"] = TimeSpan.FromDays(7), ["reminderInterval"] = TimeSpan.FromHours(24) }),
            ["check_approval"] = WorkflowBlockDefinition.Create("check_approval", "CheckApprovalBlock", "FlowCore", "route_to_department", "send_back_for_updates", new Dictionary<string, object> { ["requiredApprovers"] = new[] { "manager" }, ["minApprovals"] = 1 }),
            ["route_to_department"] = WorkflowBlockDefinition.Create("route_to_department", "RouteToDepartmentBlock", "FlowCore", "set_access", "", new Dictionary<string, object> { ["targetDepartment"] = "finance", ["routingRules"] = "auto" }),
            ["set_access"] = WorkflowBlockDefinition.Create("set_access", "SetAccessBlock", "FlowCore", "approval_complete", "", new Dictionary<string, object> { ["visibleTo"] = new[] { "creator", "manager", "department" }, ["accessLevel"] = "read" }),
            ["approval_complete"] = WorkflowBlockDefinition.Create("approval_complete", "ApprovalCompleteBlock", "FlowCore", "", "", new Dictionary<string, object> { ["sendNotification"] = true, ["notificationRecipients"] = new[] { "creator", "manager" } }),
            ["send_back_for_updates"] = WorkflowBlockDefinition.Create("send_back_for_updates", "SendBackForUpdatesBlock", "FlowCore", "update_document", "", new Dictionary<string, object> { ["reason"] = "additional_info_required", ["comments"] = "" }),
            ["update_document"] = WorkflowBlockDefinition.Create("update_document", "UpdateDocumentBlock", "FlowCore", "send_for_approval", "", new Dictionary<string, object> { ["allowResubmission"] = true, ["maxResubmissions"] = 3 }),
            ["validation_failed"] = WorkflowBlockDefinition.Create("validation_failed", "ValidationFailedBlock", "FlowCore", "", "", new Dictionary<string, object> { ["errorCode"] = "VALIDATION_ERROR", ["notifyUser"] = true }),
            ["approval_rejected"] = WorkflowBlockDefinition.Create("approval_rejected", "ApprovalRejectedBlock", "FlowCore", "", "", new Dictionary<string, object> { ["rejectionReason"] = "insufficient_info", ["allowAppeal"] = true })
        };

        var blockGuards = new Dictionary<string, IList<GuardDefinition>>
        {
            ["await_approval"] = new List<GuardDefinition> { approvalGuard }
        };

        var workflow = WorkflowDefinition.Create(
            "document-approval",
            "Document Approval Workflow",
            "set_initial_status",
            blocks,
            "1.0.0",
            "Workflow for creating, approving, and routing documents with access controls",
            WorkflowMetadata.Create(),
            WorkflowExecutionConfig.Create(),
            new Dictionary<string, object> { ["initialStatus"] = "Being Entered" },
            new List<GuardDefinition>(),
            blockGuards);

        // Execute the workflow
        var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
        var executor = serviceProvider.GetRequiredService<IWorkflowExecutor>();
        var workflowStore = serviceProvider.GetRequiredService<IWorkflowStore>();
        var parser = serviceProvider.GetRequiredService<IWorkflowParser>();
        var validator = serviceProvider.GetRequiredService<IWorkflowValidator>();
        var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);


        // Execute the workflow
        var documentData = new
        {
            DocumentId = "DOC-001",
            Title = "Sample Document",
            Content = "This is a sample document content.",
            CreatedBy = "user123",
            Status = "Being Entered",
            Comments = "",
            VisibleTo = new[] { "user123" },
            UserRoles = new[] { "manager" }, // Simulate manager approval
            ManagerRoles = new[] { "manager" },
            DepartmentRoles = new[] { "department" }
        };

        var result = await engine.ExecuteAsync(workflow, documentData);
        Console.WriteLine($"Workflow completed: {result.Succeeded}");
        object? status = null;
        object? visibleTo = null;
        result.FinalState?.TryGetValue("Status", out status);
        result.FinalState?.TryGetValue("VisibleTo", out visibleTo);
        Console.WriteLine($"Final Status: {status?.ToString() ?? "Unknown"}");
        Console.WriteLine($"Visible To: {string.Join(", ", (visibleTo as string[]) ?? [])}");
        Console.WriteLine();
    }
}