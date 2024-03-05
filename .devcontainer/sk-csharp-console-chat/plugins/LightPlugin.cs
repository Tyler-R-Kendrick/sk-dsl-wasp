using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Plugins;

/// <summary>
/// A Sematic Kernel skill that interacts with ChatGPT
/// </summary>
internal class LightPlugin
{
    public bool IsOn { get; set; } = false;

    [KernelFunction("GetState")]
    [Description("Gets the state of the light.")]
    public string GetState() => IsOn ? "on" : "off";

    [KernelFunction("ChangeState")]
    [Description("Changes the state of the light.'")]
    public string ChangeState(bool newState)
    {
        IsOn = newState;
        var state = GetState();

        // Print the state to the console
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.WriteLine($"[Light is now {state}]");
        Console.ResetColor();

        return state;
    }
}
