namespace FlowCore.Validation;

/// <summary>
/// Validates workflow definitions to ensure they meet structural and semantic requirements.
/// Performs comprehensive validation including basic structure validation, circular dependency detection,
/// and configuration validation to prevent runtime errors during workflow execution.
/// </summary>
public class WorkflowValidator : IWorkflowValidator
{
    /// <summary>
    /// Validates a workflow definition to ensure it meets all structural and semantic requirements.
    /// Performs comprehensive validation including basic properties, block references, circular dependencies,
    /// and execution configuration validation.
    /// </summary>
    /// <param name="definition">The workflow definition to validate.</param>
    /// <returns>A validation result containing any errors or warnings found during validation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
    public ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Phase 1: Basic workflow properties validation
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            errors.Add("Workflow ID cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add("Workflow name cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(definition.StartBlockName))
        {
            errors.Add("Workflow start block name cannot be null or empty.");
        }

        // Validate that the start block exists in the workflow definition
        if (!string.IsNullOrWhiteSpace(definition.StartBlockName) &&
            !definition.Blocks.ContainsKey(definition.StartBlockName))
        {
            errors.Add($"Start block '{definition.StartBlockName}' is not defined in the workflow.");
        }

        // Phase 2: Individual block validation
        foreach (var (blockName, block) in definition.Blocks)
        {
            // Validate that referenced blocks exist in the workflow definition
            if (!string.IsNullOrWhiteSpace(block.NextBlockOnSuccess) &&
                !definition.Blocks.ContainsKey(block.NextBlockOnSuccess))
            {
                errors.Add($"Block '{blockName}' references non-existent success block '{block.NextBlockOnSuccess}'.");
            }

            if (!string.IsNullOrWhiteSpace(block.NextBlockOnFailure) &&
                !definition.Blocks.ContainsKey(block.NextBlockOnFailure))
            {
                errors.Add($"Block '{blockName}' references non-existent failure block '{block.NextBlockOnFailure}'.");
            }

            // Validate block type specification
            if (string.IsNullOrWhiteSpace(block.BlockType))
            {
                errors.Add($"Block '{blockName}' has empty or null block type.");
            }

            // Warn about missing assembly specification
            if (string.IsNullOrWhiteSpace(block.AssemblyName))
            {
                warnings.Add($"Block '{blockName}' has no assembly specified, using default.");
            }
        }

        // Phase 3: Guard validation
        foreach (var (blockName, guards) in definition.BlockGuards)
        {
            // Validate that guards are defined for existing blocks
            if (!definition.Blocks.ContainsKey(blockName))
            {
                errors.Add($"Block guards defined for non-existent block '{blockName}'.");
            }

            // Validate individual guard definitions
            foreach (var guard in guards)
            {
                if (string.IsNullOrWhiteSpace(guard.GuardType))
                {
                    errors.Add($"Guard '{guard.GuardId}' in block '{blockName}' has empty or null guard type.");
                }
            }
        }

        // Phase 4: Circular dependency detection using depth-first search
        var visitedBlocks = new HashSet<string>();
        var currentPath = new HashSet<string>();

        // Recursive helper method to detect circular dependencies in block transition paths
        bool HasCircularDependency(string blockName, string targetBlock)
        {
            // If target block is already in current path, we found a circular dependency
            if (currentPath.Contains(targetBlock))
                return true;

            // If we've already visited this block and no circular dependency was found, skip it
            if (visitedBlocks.Contains(targetBlock))
                return false;

            // If target block doesn't exist in workflow, no circular dependency possible
            if (!definition.Blocks.TryGetValue(targetBlock, out var block))
                return false;

            // Add current block to path and recursively check its transitions
            currentPath.Add(targetBlock);
            var hasCircular = HasCircularDependency(targetBlock, block.NextBlockOnSuccess ?? string.Empty) ||
                             HasCircularDependency(targetBlock, block.NextBlockOnFailure ?? string.Empty);
            currentPath.Remove(targetBlock);

            return hasCircular;
        }

        // Check each block for circular dependencies in both success and failure paths
        foreach (var (blockName, block) in definition.Blocks)
        {
            visitedBlocks.Add(blockName);
            currentPath.Clear();

            if (HasCircularDependency(blockName, block.NextBlockOnSuccess ?? string.Empty))
            {
                errors.Add($"Circular dependency detected involving block '{blockName}' and its successors.");
            }

            if (HasCircularDependency(blockName, block.NextBlockOnFailure ?? string.Empty))
            {
                errors.Add($"Circular dependency detected involving block '{blockName}' and its failure path.");
            }
        }

        // Phase 5: Execution configuration validation
        if (definition.ExecutionConfig.Timeout <= TimeSpan.Zero)
        {
            errors.Add("Workflow timeout must be greater than zero.");
        }

        if (definition.ExecutionConfig.MaxConcurrentBlocks < 1)
        {
            errors.Add("Maximum concurrent blocks must be at least 1.");
        }

        // Validate retry policy configuration
        if (definition.ExecutionConfig.RetryPolicy.MaxRetries < 0)
        {
            errors.Add("Maximum retry attempts cannot be negative.");
        }

        if (definition.ExecutionConfig.RetryPolicy.InitialDelay < TimeSpan.Zero)
        {
            errors.Add("Initial retry delay cannot be negative.");
        }

        // Warn about potentially problematic retry configuration
        if (definition.ExecutionConfig.RetryPolicy.MaxDelay < definition.ExecutionConfig.RetryPolicy.InitialDelay)
        {
            warnings.Add("Maximum retry delay is less than initial delay.");
        }

        // Phase 6: Return validation result based on findings
        if (errors.Count != 0)
        {
            return ValidationResult.Failure(errors);
        }

        if (warnings.Count != 0)
        {
            return ValidationResult.WithWarnings(warnings);
        }

        return ValidationResult.Success();
    }
}