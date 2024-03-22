using System.Reactive.Linq;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Plugins;

public abstract class AIChat(
    TextReader reader,
    TextWriter writer,
    IChatCompletionService completions,
    ILogger<AIChat> logger,
    ChatHistory history)
    : BackgroundService
{
    protected abstract string SystemPrompt { init; get; }

    protected override sealed async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("Starting Chat Session...");
        history.AddSystemMessage(SystemPrompt);

        OpenAIPromptExecutionSettings settings = new()
        {
            ChatSystemPrompt = SystemPrompt,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };
        while (!cancellationToken.IsCancellationRequested)
        {
            // Get user input
            var userPrompt = await GetUserInputAsync(cancellationToken);
            await HandleUserInputAsync(userPrompt, history, cancellationToken);
            // Get the chat completions
            var assistantMessage = await GetAssistantOutputAsync(completions,
                history, settings, cancellationToken);
            await HandleAssistantOutputAsync(assistantMessage,
                history, cancellationToken);
        }
    }

    protected virtual async Task<string> GetUserInputAsync(
        CancellationToken cancellationToken)
    {
        await WriteAsync("User > ");
        return (await reader.ReadLineAsync(cancellationToken)) ?? string.Empty;
    }

    protected virtual Task HandleUserInputAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        history.AddUserMessage(message);
        return Task.CompletedTask;
    }

    protected virtual async Task<string> GetAssistantOutputAsync(
        IChatCompletionService chatCompletionService,
        ChatHistory history,
        OpenAIPromptExecutionSettings openAIPromptExecutionSettings,
        CancellationToken cancellationToken)
    {
        var results = chatCompletionService
            .GetStreamingChatMessageContentsAsync(
                history,
                executionSettings: openAIPromptExecutionSettings,
                cancellationToken: cancellationToken);
        return await ProcessResults(results, cancellationToken);
    }

    protected virtual Task HandleAssistantOutputAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        history.AddAssistantMessage(message);
        return Task.CompletedTask;
    }

    private async Task<string> ProcessResults(
        IAsyncEnumerable<StreamingChatMessageContent> results,
        CancellationToken cancellationToken)
    {
        // Print the chat completions
        StringBuilder content = new();
        logger.LogTrace("Processing Results");
        try
        {
            await foreach (var streamContent in results.WithCancellation(cancellationToken))
            {
                if(streamContent.Role == AuthorRole.Assistant)
                {
                    await WriteAsync("Assistant > ");
                }
                var contentMessage = streamContent.Content ?? string.Empty;
                content.Append(contentMessage);
                await WriteAsync(contentMessage);
            }
            logger.LogTrace("Processed Results");
            await WriteLineAsync();
            return content.ToString();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, $"The operation was cancelled.");
            return ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, $"An error occurred.");
            return ex.Message;
        }
    }

    protected async Task WriteAsync(string? message = null)
        => await writer.WriteAsync(message);
    protected async Task WriteLineAsync(string? message = null)
        => await writer.WriteLineAsync(message);
}
