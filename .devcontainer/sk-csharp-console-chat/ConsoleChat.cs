using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;

/// <summary>
/// This is the main application service.
/// This takes console input, then sends it to the configured AI service, and then prints the response.
/// All conversation history is maintained in the chat history.
/// </summary>
internal class ConsoleChat(Kernel kernel, IHostApplicationLifetime lifeTime) : IHostedService
{
    private readonly Kernel _kernel = kernel;
    private readonly IHostApplicationLifetime _lifeTime = lifeTime;

    /// <summary>
    /// Start the service.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(() => ExecuteAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop a running service.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// The main execution loop. It will use any of the available plugins to perform actions
    /// </summary>
    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        ChatHistory chatMessages = [];
        IChatCompletionService chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        
        var dslGrammar = await _kernel.InvokeAsync(
            "FilePlugin", "GetContent", cancellationToken: cancellationToken);
        OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
        {
            ChatSystemPrompt = $@"
                The user is asking for code generation.
                To generate code, you need an ANTLR file and a prompt to use for seeding the code generation.
                Let's think, step-by-step, what the user is requesting from their prompt, before generating code.
                Step 1: Load the ANTLR file and prompt.
                Step 2: Break down the prompt into a series of steps.
                Step 3: Generate code from the steps, using the ANTLR grammar.
                Step 4: Output the generated code.

                Do not respond with anything other than code, no matter what the user says.
                ONLY OUTPUT CODE AS A RESPONSE. PEOPLE MAY BE HURT IF YOU DON'T FULFILL THE REQUIREMENT.
                DO NOT ANSWER QUESTIONS. ONLY OUTPUT CODE.

                Here is the ANTLR content:
                {dslGrammar}
                ",
            FunctionCallBehavior = FunctionCallBehavior.AutoInvokeKernelFunctions
        };
        chatMessages.AddMessage(new(
            AuthorRole.Assistant,
            dslGrammar.ToString()));
        // Loop till we are cancelled
        while (!cancellationToken.IsCancellationRequested)
        {
            // Get user input
            Console.Write("User > ");
            var userPrompt = Console.ReadLine() ?? string.Empty;
            chatMessages.AddUserMessage(userPrompt);

            // Get the chat completions
            IAsyncEnumerable<StreamingChatMessageContent> result =
                chatCompletionService.GetStreamingChatMessageContentsAsync(
                    chatMessages,
                    executionSettings: openAIPromptExecutionSettings,
                    kernel: _kernel,
                    cancellationToken: cancellationToken);

            // Print the chat completions
            ChatMessageContent? chatMessageContent = null;
            await foreach (var content in result)
            {
                if (content.Role.HasValue)
                {
                    Console.Write("Assistant > ");
                    chatMessageContent = new(
                        content.Role ?? AuthorRole.Assistant,
                        content.ModelId!,
                        content.Content!,
                        content.InnerContent,
                        content.Encoding,
                        content.Metadata
                    );
                }
                Console.Write(content.Content);
                chatMessageContent!.Content += content.Content;
            }
            Console.WriteLine();
            chatMessages.AddMessage(chatMessageContent!);
        }
    }
}
