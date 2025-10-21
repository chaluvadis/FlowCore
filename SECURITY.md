# Security Best Practices and Safe Extension Points

## Overview

This document outlines the security hardening measures implemented in the LinkedListWorkflowEngine and provides guidance for safely extending the system with custom workflow blocks.

## Security Features

### Dynamic Assembly Loading Protection

The WorkflowBlockFactory now includes comprehensive security measures to prevent malicious assembly loading:

#### 1. Assembly Loading Controls

**Default Security Setting:**
```csharp
// Dynamic assembly loading is DISABLED by default for security
var factory = new WorkflowBlockFactory(serviceProvider); // Secure by default
```

**Explicit Opt-in Required:**
```csharp
// Must explicitly enable dynamic assembly loading
var securityOptions = new WorkflowBlockFactorySecurityOptions
{
    AllowDynamicAssemblyLoading = true,
    AllowedAssemblyNames = new[] { "MyCompany.WorkflowBlocks", "TrustedBlocks" },
    ValidateStrongNameSignatures = true
};

var factory = new WorkflowBlockFactory(serviceProvider, securityOptions);
```

#### 2. Assembly Whitelist Protection

Only assemblies explicitly listed in the whitelist can be loaded:

```csharp
var securityOptions = new WorkflowBlockFactorySecurityOptions
{
    AllowDynamicAssemblyLoading = true,
    AllowedAssemblyNames = new[] {
        "MyCompany.WorkflowBlocks",
        "TrustedBlocks.Library",
        "ApprovedExtensions"
    }
};
```

#### 3. Strong-Name Signature Validation

All dynamically loaded assemblies must have valid strong-name signatures:

```csharp
var securityOptions = new WorkflowBlockFactorySecurityOptions
{
    AllowDynamicAssemblyLoading = true,
    ValidateStrongNameSignatures = true,
    AllowedPublicKeyTokens = new[] {
        new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }, // Your company's key
        new byte[] { 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90 }  // Trusted partner's key
    }
};
```

## Safe Extension Points

### 1. Creating Custom Workflow Blocks

**Secure Implementation Pattern:**
```csharp
using LinkedListWorkflowEngine.Core.Interfaces;

public class MyCustomBlock : IWorkflowBlock
{
    private readonly ILogger<MyCustomBlock> _logger;

    public MyCustomBlock(ILogger<MyCustomBlock> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string NextBlockOnSuccess => "next_block_name";
    public string NextBlockOnFailure => "error_block_name";

    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        try
        {
            // Validate input security
            if (!IsInputSecure(context.Input))
            {
                return ExecutionResult.Failure("security_validation_failed");
            }

            // Perform business logic
            var result = await PerformSecureOperation(context);

            return ExecutionResult.Success(NextBlockOnSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MyCustomBlock");
            return ExecutionResult.Failure(NextBlockOnFailure);
        }
    }

    private bool IsInputSecure(object input)
    {
        // Implement security validation logic
        return input != null && IsValidInputType(input);
    }

    private async Task<object> PerformSecureOperation(ExecutionContext context)
    {
        // Implement your business logic with security considerations
        return await Task.FromResult(new { Processed = true });
    }
}
```

### 2. Dependency Injection Registration

**Secure Service Registration:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Register your custom blocks for DI resolution
    services.AddTransient<MyCustomBlock>();
    services.AddTransient<IWorkflowBlock, MyCustomBlock>(sp =>
        sp.GetRequiredService<MyCustomBlock>());

    // Configure security options
    services.Configure<WorkflowBlockFactorySecurityOptions>(options =>
    {
        options.AllowDynamicAssemblyLoading = false; // Disable for maximum security
        options.ValidateStrongNameSignatures = true;
    });

    // Register factory with security options
    services.AddTransient<IWorkflowBlockFactory>(sp =>
    {
        var securityOptions = sp.GetRequiredService<IOptions<WorkflowBlockFactorySecurityOptions>>().Value;
        return new WorkflowBlockFactory(sp, securityOptions, sp.GetService<ILogger<WorkflowBlockFactory>>());
    });
}
```

### 3. JSON Workflow Definitions (Secure Approach)

**Instead of dynamic assembly loading, use pre-registered types:**

```json
{
  "workflow": {
    "id": "secure_workflow",
    "nodes": {
      "custom_processing": {
        "type": "MyCompany.WorkflowBlocks.MyCustomBlock, MyCompany.WorkflowBlocks",
        "configuration": {
          "setting1": "value1",
          "setting2": "value2"
        },
        "transitions": {
          "success": "next_node",
          "failure": "error_handler"
        }
      }
    }
  }
}
```

## Security Best Practices

### 1. Assembly Management

**DO:**
- Use strong-name signing for all custom assemblies
- Register custom blocks through dependency injection
- Validate all inputs in custom blocks
- Implement proper error handling and logging

**DON'T:**
- Load assemblies dynamically from untrusted sources
- Use weak assembly references
- Skip input validation in custom blocks
- Ignore security warnings in logs

### 2. Configuration Security

**Secure Configuration Pattern:**
```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Load configuration from secure sources only
        var securityConfig = LoadSecurityConfiguration();

        services.Configure<WorkflowBlockFactorySecurityOptions>(options =>
        {
            options.AllowDynamicAssemblyLoading = securityConfig.AllowDynamicLoading;
            options.AllowedAssemblyNames = securityConfig.AllowedAssemblies;
            options.ValidateStrongNameSignatures = true;
            options.AllowedPublicKeyTokens = securityConfig.TrustedKeys;
        });
    }

    private WorkflowBlockFactorySecurityOptions LoadSecurityConfiguration()
    {
        // Load from encrypted configuration files, environment variables, or secure stores
        return new WorkflowBlockFactorySecurityOptions
        {
            AllowDynamicAssemblyLoading = false, // Default to secure
            AllowedAssemblyNames = new[] { "MyCompany.TrustedBlocks" },
            ValidateStrongNameSignatures = true
        };
    }
}
```

### 3. Runtime Security Validation

**Implement Runtime Checks:**
```csharp
public class SecureWorkflowBlock : IWorkflowBlock
{
    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        // Runtime security validation
        if (!await ValidateExecutionEnvironment())
        {
            return ExecutionResult.Failure("environment_validation_failed");
        }

        if (!await ValidateUserPermissions(context))
        {
            return ExecutionResult.Failure("permission_denied");
        }

        // Proceed with business logic only if security checks pass
        return await ExecuteBusinessLogic(context);
    }
}
```

## Migration Guide

### From Insecure to Secure Implementation

**Before (Insecure):**
```csharp
// ❌ Insecure: No restrictions on assembly loading
var factory = new WorkflowBlockFactory(serviceProvider);
```

**After (Secure):**
```csharp
// ✅ Secure: Explicit security configuration
var securityOptions = new WorkflowBlockFactorySecurityOptions
{
    AllowDynamicAssemblyLoading = false, // Disable dynamic loading
    ValidateStrongNameSignatures = true
};

var factory = new WorkflowBlockFactory(serviceProvider, securityOptions);
```

### Updating Existing Workflows

1. **Identify Dynamic Assembly Usage:**
   - Review all workflow definitions for dynamic assembly references
   - Replace with pre-registered types where possible

2. **Update Security Configuration:**
   - Add security options to all factory instantiations
   - Configure appropriate whitelists for your environment

3. **Test Security Measures:**
   - Verify that unauthorized assemblies cannot be loaded
   - Test that security exceptions are properly handled
   - Ensure legitimate workflows continue to function

## Monitoring and Alerting

### Security Event Logging

```csharp
public class SecurityAwareWorkflowBlock : IWorkflowBlock
{
    private readonly ILogger _logger;

    public async Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        // Log security-relevant events
        _logger.LogInformation("Executing block {BlockName} for user {UserId}",
            nameof(SecureWorkflowBlock), GetCurrentUserId());

        try
        {
            var result = await PerformSecureOperation(context);

            _logger.LogInformation("Block {BlockName} completed successfully",
                nameof(SecureWorkflowBlock));

            return ExecutionResult.Success();
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "Security violation in block {BlockName}",
                nameof(SecureWorkflowBlock));

            return ExecutionResult.Failure("security_violation");
        }
    }
}
```

## Compliance Considerations

### Security Headers and Standards

- **Strong-Name Signing:** All custom assemblies must be strong-name signed
- **Assembly Whitelisting:** Only explicitly allowed assemblies can be loaded
- **Input Validation:** All block inputs must be validated for security
- **Error Handling:** Security exceptions should not expose sensitive information
- **Logging:** Security events must be logged for audit purposes

### Audit Trail Requirements

The security implementation provides comprehensive audit trails:
- Assembly loading attempts (successful and failed)
- Security validation results
- Block execution with security context
- Configuration changes and security policy updates

## Troubleshooting

### Common Security Issues

1. **"Assembly not in allowed list"**
   - Add the assembly to the `AllowedAssemblyNames` list
   - Ensure proper spelling and case matching

2. **"Strong-name signature validation failed"**
   - Verify the assembly is strong-name signed
   - Check that the public key token is in the allowed list

3. **"Dynamic assembly loading disabled"**
   - Set `AllowDynamicAssemblyLoading = true` if dynamic loading is required
   - Consider using dependency injection registration instead

### Debug Mode

For development environments, you can enable debug logging:

```csharp
var securityOptions = new WorkflowBlockFactorySecurityOptions
{
    AllowDynamicAssemblyLoading = true, // Enable for development
    ValidateStrongNameSignatures = false, // Disable for development
    AllowedAssemblyNames = new[] { "*" } // Allow all for development
};
```

**⚠️ Warning:** Never use debug security settings in production environments.

## Support

For security-related questions or issues:
1. Review this documentation thoroughly
2. Check the security configuration in your application
3. Verify that all custom assemblies follow the security guidelines
4. Contact the security team for enterprise-specific requirements