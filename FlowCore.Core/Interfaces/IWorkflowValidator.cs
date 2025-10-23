namespace FlowCore.Interfaces;

/// <summary>
/// Defines the contract for workflow validators that ensure workflow definitions meet
/// structural and semantic requirements before execution.
/// </summary>
public interface IWorkflowValidator
{
    /// <summary>
    /// Validates a workflow definition to ensure it meets all requirements.
    /// </summary>
    /// <param name="definition">The workflow definition to validate.</param>
    /// <returns>A validation result containing any errors or warnings found during validation.</returns>
    ValidationResult Validate(WorkflowDefinition definition);
}

public record ValidationResult
{
    public bool IsValid { get; init; }

    public IEnumerable<string> Errors { get; init; } = [];

    public IEnumerable<string> Warnings { get; init; } = [];

    public static ValidationResult Success()
        => new() { IsValid = true };

    public static ValidationResult Failure(IEnumerable<string> errors)
        => new() { IsValid = false, Errors = errors };

    public static ValidationResult WithWarnings(IEnumerable<string> warnings)
        => new() { IsValid = true, Warnings = warnings };
}