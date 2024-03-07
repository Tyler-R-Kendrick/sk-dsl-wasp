using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Plugins;

internal class FilePlugin
{
    public string Content { get; set; } = string.Empty;
    private const string DefaultPath = "grammars/taxDSL.g4";

    [KernelFunction]
    [Description("Gets the contents of a file from an optional filePath.")]
    public string GetContent(string? filePath = DefaultPath)
    {
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.WriteLine("[Retrieving file contents]");
        Console.ResetColor();
        return Content == string.Empty ? LoadContent(filePath) : Content;
    }

    [KernelFunction]
    [Description("Loads a file from an optional filePath.")]
    public string LoadContent(string? filePath = DefaultPath)
    {
        try
        {
            filePath ??= DefaultPath;
            Content = File.ReadAllText(filePath);
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("[Loaded file contents]");
        }
        catch(Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{e.Message}]");
            return "There was an error.";
        }
        finally
        {
            Console.ResetColor();
        }
        return Content;
    }
}
