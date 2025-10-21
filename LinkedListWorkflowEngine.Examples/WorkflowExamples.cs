namespace LinkedListWorkflowEngine.Examples;
using LinkedListWorkflowEngine.Core.Parsing;

/// <summary>
/// Examples demonstrating the workflow engine functionality.
/// </summary>
public static class WorkflowExamples
{
    /// <summary>
    /// Example showing the WorkflowDefinitionParser with enhanced error reporting.
    /// </summary>
    public static async Task RunParserExample()
    {
        Console.WriteLine("=== Enhanced Parser Example ===");

        // Example of using the WorkflowDefinitionParser for detailed error reporting
        var parser = new WorkflowDefinitionParser();

        // JSON with syntax error
        var jsonWithSyntaxError = """
            {
                "id": "syntax-error-workflow",
                "name": "Workflow with Syntax Error",
                "startBlockName": "start",
                "blocks": {
                    "start": {
                        "id": "start",
                        "type": "BasicBlocks.LogBlock",
                        "assembly": "LinkedListWorkflowEngine.Core",
                        "nextBlockOnSuccess": "end"
                    }
                }
            """; // Missing closing brace

        Console.WriteLine("Testing JSON with syntax error...");
        try
        {
            var definition = parser.ParseFromJson(jsonWithSyntaxError);
            Console.WriteLine("Parsed successfully");
        }
        catch (WorkflowParseException ex)
        {
            Console.WriteLine($"Caught syntax error:");
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        // JSON with logical error (missing start block)
        var jsonWithLogicalError = """
            {
                "id": "logical-error-workflow",
                "name": "Workflow with Logical Error",
                "startBlockName": "nonexistent-block",
                "blocks": {
                    "start": {
                        "id": "start",
                        "type": "BasicBlocks.LogBlock",
                        "assembly": "LinkedListWorkflowEngine.Core",
                        "nextBlockOnSuccess": "end"
                    }
                }
            }
            """;

        Console.WriteLine("\nTesting JSON with logical error...");
        try
        {
            var definition = parser.ParseFromJson(jsonWithLogicalError);
            Console.WriteLine("Parsed successfully");
        }
        catch (WorkflowParseException ex)
        {
            Console.WriteLine($"Caught logical error:");
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine("\nParser benefits:");
        Console.WriteLine("✓ Detailed error location (line/column)");
        Console.WriteLine("✓ Specific error categorization (syntax vs logical)");
        Console.WriteLine("✓ Actionable user guidance");
        Console.WriteLine("✓ Comprehensive validation");
    }
}