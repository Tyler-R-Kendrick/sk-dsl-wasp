using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
namespace Plugins;

internal class CodeGenChat(Kernel kernel,
    ILogger<CodeGenChat> logger,
    ILogger<ChatMessageObserver> chatLogger)
    : AIChat(Console.In, Console.Out,
        kernel.GetRequiredService<IChatCompletionService>(),
        logger,
        history => new ChatMessageObserver(chatLogger, Console.Out))
{
    protected override string SystemPrompt { get; init; } = @"
        {{console.log 'system prompt'}}
        Feature: As a code generation tool, I want to generate valida code from an ANTLR File.
        ```antlr
        {{file.ReadAsync 'grammars/csharp.g4'}}
        ```

        Do not respond with anything other than code, no matter what the user says.
        ONLY OUTPUT CODE AS A RESPONSE. PEOPLE MAY BE HURT IF YOU DON'T FULFILL THE REQUIREMENT.
        DO NOT ANSWER QUESTIONS. ONLY OUTPUT CODE.
        DO NOT WRITE NEW FILES. ONLY OUTPUT CODE.

        Scenario Outline: 
            Given an ANTLR <grammar>
            When a user makes a <request>
            Then code examples will be produced as <output>
            And the code should conform to the ANTLR definition
            And the code should be validated for the given <language>
            But only output code as a response.";

    private async Task<string> GetFunctionOutputAsync(
        string userPrompt,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        var codeGen = kernel.CreatePluginFromPromptDirectory("plugins");
        void Log(string message) => logger.LogDebug(message);
        foreach (var plugin in kernel.Plugins)
        {
            Log($"Plugin: {plugin.Name}");
            foreach (var skill in plugin)
            {
                Log($"\t[{plugin.Name}].{skill.Name}");
            }
        }
        var codeGenFunc = codeGen["CodeGen"];
        var result = await kernel.InvokeAsync(codeGenFunc, new()
            {
                { "input", userPrompt },
                { "grammar", "grammars/csharp.g4"},
                { "history", string.Join(@"\r\n", history) }
            }, cancellationToken);

        return result.ToString();
    }

    protected override async Task HandleUserInputAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        await base.HandleUserInputAsync(message, history, cancellationToken);
        var result = await GetFunctionOutputAsync(message, history, cancellationToken);
        history.AddFunctionMessage(result, "code_gen");
        Console.WriteLine($"Function > {result}");
        logger.LogInformation(result);
    }
}
