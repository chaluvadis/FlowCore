namespace LinkedListWorkflowEngine.Core.Interfaces;
/// <summary>
/// Factory interface for creating workflow blocks.
/// </summary>
public interface IWorkflowBlockFactory
{
    IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition);
}