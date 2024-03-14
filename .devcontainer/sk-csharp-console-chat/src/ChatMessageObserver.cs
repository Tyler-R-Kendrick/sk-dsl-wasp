using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
namespace Plugins;

internal class ChatMessageObserver(
    ILogger<ChatMessageObserver> logger,
    TextWriter writer)
    : IObserver<ChatMessageContent>
{
    public void OnCompleted()
    {
        logger.LogTrace("Chat Message Observer Completed");
    }

    public void OnError(Exception error)
    {
        logger.LogError(error, "Chat Message Observer Error");
        writer.WriteLine(error.Message);
    }

    public void OnNext(ChatMessageContent value)
    {
        logger.LogTrace($"Chat Message Observer Next: {value}");
        var role = value.Role switch
        {
            var assistant when assistant == AuthorRole.Assistant => "Assistant",
            var user when user == AuthorRole.User => "User",
            var system when system == AuthorRole.System => "System",
            var tool when tool == AuthorRole.Tool => "Tool",
            _ => "Unknown"
        };
        switch(role)
        {
            case "User":
                break;
            case "Assistant":
            case "System":
            case "Tool":
                writer.WriteLine($"{role} > {value.Content}");
                break;
            default:
                writer.WriteLine($"Unknown > {value.Content}");
                break;
        }
    }
}
