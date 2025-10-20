namespace LinkedListWorkflowEngine.Core.Interfaces;

/// <summary>
/// Factory interface for creating workflow blocks.
/// </summary>
public interface IWorkflowBlockFactory
{
    /// <summary>
    /// Creates a workflow block from its definition.
    /// </summary>
    /// <param name="blockDefinition">The definition of the block to create.</param>
    /// <returns>The created workflow block, or null if creation failed.</returns>
    IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition);
}