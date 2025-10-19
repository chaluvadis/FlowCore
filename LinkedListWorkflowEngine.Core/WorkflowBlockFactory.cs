public class WorkflowBlockFactory(IServiceProvider serviceProvider) : IWorkflowBlockFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    public IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition)
    {
        try
        {
            // For now, we'll implement a simple factory that creates blocks by type name
            // TODO: In a real implementation, this would use reflection or a DI container
            var blockType = Type.GetType($"{blockDefinition.Namespace ?? "LinkedListWorkflowEngine.Core.Blocks"}.{blockDefinition.BlockType}, {blockDefinition.AssemblyName}")
                ?? Type.GetType($"{blockDefinition.BlockType}, {blockDefinition.AssemblyName}");

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