namespace FlowCore.Interfaces;

/// <summary>
/// Defines the contract for workflow parsers that convert workflow definitions from various formats
/// into structured workflow objects. Implementations handle parsing from JSON strings and files.
/// </summary>
public interface IWorkflowParser
{
    /// <summary>
    /// Parses a JSON string into a workflow definition object.
    /// </summary>
    /// <param name="json">The JSON string containing the workflow definition.</param>
    /// <returns>The parsed workflow definition object.</returns>
    /// <exception cref="WorkflowParseException">Thrown when JSON parsing fails or the structure is invalid.</exception>
    WorkflowDefinition ParseFromJson(string json);

    /// <summary>
    /// Parses a workflow definition from a file containing JSON content.
    /// </summary>
    /// <param name="path">The file system path to the JSON workflow definition file.</param>
    /// <returns>A task representing the parsed workflow definition object.</returns>
    /// <exception cref="WorkflowParseException">Thrown when file reading fails or JSON parsing fails.</exception>
    Task<WorkflowDefinition> ParseFromFileAsync(string path);
}

public class WorkflowParseException : Exception
{
    public WorkflowParseException(string message) : base(message) { }
    public WorkflowParseException(string message, Exception innerException) : base(message, innerException) { }
}