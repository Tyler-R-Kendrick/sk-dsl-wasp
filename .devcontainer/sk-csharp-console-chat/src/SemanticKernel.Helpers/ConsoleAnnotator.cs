namespace Plugins;

public static class ConsoleAnnotator
{
    public static string WriteLine(string message, ConsoleColor color = ConsoleColor.DarkBlue)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{message}]");
        Console.ResetColor();
        return message;
    }
}
