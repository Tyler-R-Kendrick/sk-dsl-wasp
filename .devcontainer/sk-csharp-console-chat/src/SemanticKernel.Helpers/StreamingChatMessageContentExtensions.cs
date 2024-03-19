using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
namespace Plugins;

internal static class StreamingChatMessageContentExtensions
{
    public static ChatMessageContent ToChatMessageContent(
        this StreamingChatMessageContent content) => new(
            content.Role ?? AuthorRole.System,
            content.ModelId!,
            content.Content!,
            content.InnerContent,
            content.Encoding,
            content.Metadata);
}
