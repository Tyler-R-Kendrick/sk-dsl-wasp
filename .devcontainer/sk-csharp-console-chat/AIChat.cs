using System.Reactive.Linq;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
namespace Plugins;

internal abstract class AIChat(
    TextReader reader,
    TextWriter writer,
    IChatCompletionService completions,
    ILogger<AIChat> logger,
    Func<ChatHistory, IObserver<ChatMessageContent>> messageObserverFactory)
    : BackgroundService
{
    protected abstract string SystemPrompt { init; get; }

    private IObservable<string> GetUserPrompts(ChatHistory history, CancellationToken cancellationToken)
        => Observable.FromAsync(
            async () =>
            {
                Console.Write("User > ");
                var prompt = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(true) ?? string.Empty;
                history.AddUserMessage(prompt);
                return prompt;
            });

    private IObservable<string> GetAssistantResponses(ChatHistory history, CancellationToken cancellationToken)
    {
        var observableAssistantPrompts = Observable.FromAsync(
            async () =>
            {
                var results = completions.GetStreamingChatMessageContentsAsync(
                    history,
                    executionSettings: new OpenAIPromptExecutionSettings
                    {
                        ChatSystemPrompt = SystemPrompt,
                        FunctionCallBehavior = FunctionCallBehavior.AutoInvokeKernelFunctions
                    },
                    cancellationToken: cancellationToken);
                return await ProcessResults(results, cancellationToken);
            });
        return observableAssistantPrompts;
    }

    protected override sealed async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("AI Chat");
        logger.LogTrace("Starting Chat Session...");
        logger.LogInformation("Info Starting Chat Session...");
        logger.LogWarning("Warning Starting Chat Session...");
        logger.LogCritical("Critical Starting Chat Session...");
        logger.LogDebug("Debug Starting Chat Session...");
        ChatHistory history = [];

        OpenAIPromptExecutionSettings settings = new()
        {
            ChatSystemPrompt = SystemPrompt,
            FunctionCallBehavior = FunctionCallBehavior.AutoInvokeKernelFunctions
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
        writer.Write("User > ");
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
                    Console.Write("Assistant > ");
                }
                var contentMessage = streamContent.Content ?? string.Empty;
                content.Append(contentMessage);
                Console.Write(contentMessage);
            }
            logger.LogTrace("Processed Results");
            Console.WriteLine();
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
}
