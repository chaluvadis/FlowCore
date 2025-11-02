namespace FlowCore.Interfaces;
/// <summary>
/// Defines the contract for workflow engines that can execute workflows defined in JSON format.
/// Extends the base workflow engine interface with JSON-specific functionality.
/// </summary>
public interface IJsonWorkflowEngine : IWorkflowEngine
{
    /// <summary>
    /// Executes a workflow from a JSON definition string.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition string.</param>
    /// <param name="input">The input data for the workflow.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the workflow execution result.</returns>
    Task<WorkflowExecutionResult> ExecuteFromJsonAsync(
        string jsonDefinition,
        object input,
        CancellationToken ct = default);
    /// <summary>
    /// Executes a workflow from a JSON file.
    /// </summary>
    /// <param name="jsonFilePath">Path to the JSON workflow definition file.</param>
    /// <param name="input">The input data for the workflow.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the workflow execution result.</returns>
    Task<WorkflowExecutionResult> ExecuteFromJsonFileAsync(
        string jsonFilePath,
        object input,
        CancellationToken ct = default);
    /// <summary>
    /// Parses a JSON workflow definition string into a WorkflowDefinition object.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition string.</param>
    /// <returns>The parsed WorkflowDefinition object.</returns>
    WorkflowDefinition ParseWorkflowDefinition(string jsonDefinition);
    /// <summary>
    /// Validates a JSON workflow definition string without executing it.
    /// </summary>
    /// <param name="jsonDefinition">The JSON workflow definition string.</param>
    /// <returns>True if the definition is valid, false otherwise.</returns>
    bool ValidateJsonDefinition(string jsonDefinition);
}
