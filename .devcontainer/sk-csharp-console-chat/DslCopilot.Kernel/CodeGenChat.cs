using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Plugins;
using static System.Environment;

public class CodeGenChat(Kernel kernel,
    PluginsFunctionsFacade plugins,
    ILogger<CodeGenChat> logger)
    : AIChat(Console.In, Console.Out,
        kernel.GetRequiredService<IChatCompletionService>(),
        logger)
{
    protected override string SystemPrompt { get; init; } = @"
        Feature: As a code generation tool, I want to generate valida code from an ANTLR File.

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

    protected override async Task HandleUserInputAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        await base.HandleUserInputAsync(message, history, cancellationToken);
        var functionResult = await plugins.GetCode(message, history, cancellationToken);
        var output = functionResult.ToString().ReplaceLineEndings(NewLine).Normalize();
        history.AddSystemMessage(output);
    }
}
