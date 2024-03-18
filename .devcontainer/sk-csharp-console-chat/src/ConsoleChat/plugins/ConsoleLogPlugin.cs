using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Plugins;

using static ConsoleColor;

[Description("Logs a message to the console.")]
internal class ConsoleLogPlugin
{
    [KernelFunction]
    [Description("Logs a message to the console.")]
    public string Log(string message, string level = "info")
    {
        ConsoleAnnotator.WriteLine(message, level switch
        {
            "info" => DarkBlue,
            "warning" => Yellow,
            "error" => Red,
            _ => DarkGray
        });
        return message;
    }
}