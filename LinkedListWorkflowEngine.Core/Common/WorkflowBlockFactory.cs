namespace LinkedListWorkflowEngine.Core.Common;
/// <summary>
/// Default implementation of the workflow block factory.
/// </summary>
public class WorkflowBlockFactory : IWorkflowBlockFactory
{
    private readonly IServiceProvider _serviceProvider;
    /// <summary>
    /// Initializes a new instance of the WorkflowBlockFactory class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    public WorkflowBlockFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }
    /// <summary>
    /// Creates a workflow block from its definition.
    /// </summary>
    /// <param name="blockDefinition">The definition of the block to create.</param>
    /// <returns>The created workflow block, or null if creation failed.</returns>
    public IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition)
    {
        try
        {
            // PLACEHOLDER: Enhanced block factory implementation needed
            // TODO: Implement proper reflection-based block creation
            // TODO: Add assembly scanning and registration
            // TODO: Support configuration injection from blockDefinition.Configuration
            // TODO: Add caching for improved performance
            // For now, we'll implement a simple factory that creates blocks by type name
            // In a real implementation, this would use reflection or a DI container
            var blockType = Type.GetType($"{blockDefinition.Namespace ?? "LinkedListWorkflowEngine.Core.Blocks"}.{blockDefinition.BlockType}, {blockDefinition.AssemblyName}");
            if (blockType == null)
            {
                // Try without namespace
                blockType = Type.GetType($"{blockDefinition.BlockType}, {blockDefinition.AssemblyName}");
            }
            if (blockType == null)
            {
                throw new TypeLoadException($"Could not load type '{blockDefinition.BlockType}' from assembly '{blockDefinition.AssemblyName}'");
            }
            // Create instance using service provider for DI support
            var block = _serviceProvider.GetService(blockType) as IWorkflowBlock;
            if (block == null)
            {
                // Fallback to Activator if not registered in DI
                block = Activator.CreateInstance(blockType, _serviceProvider.GetService(typeof(ILogger<>).MakeGenericType(blockType))) as IWorkflowBlock;
            }
            return block;
        }
        catch (Exception ex)
        {
            // Log the error and return null
            var logger = _serviceProvider.GetService(typeof(ILogger<WorkflowBlockFactory>)) as ILogger;
            logger?.LogError(ex, "Failed to create block of type '{BlockType}' from assembly '{AssemblyName}'",
                blockDefinition.BlockType, blockDefinition.AssemblyName);
            return null;
        }
    }
}