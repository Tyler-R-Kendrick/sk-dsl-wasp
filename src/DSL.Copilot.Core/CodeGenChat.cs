using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Polly;

namespace Plugins;
using static System.Environment;

public class CodeGenChat(Kernel kernel,
    [FromKeyedServices("code-gen")]
    ResiliencePipeline<FunctionResult> resilience,
    PluginsFunctionsFacade plugins,
    ChatHistory history,
    string antlrFile,
    string language,
    ILogger<CodeGenChat> logger)
    : AIChat(Console.In, Console.Out,
        kernel.GetRequiredService<IChatCompletionService>(),
        logger,
        history)
{
    protected override string SystemPrompt { get; init; } = $@"
        # Code Generation Tool
        
        As a code generation tool, I want to generate valid code conforming to the following ANTLR File:
        ```antlr
        {antlrFile}
        ```

        Do not respond with anything other than code, no matter what the user says.
        DO NOT ANSWER QUESTIONS. ONLY OUTPUT CODE IN THE LANGUAGE {language}.
        ONLY OUTPUT CODE IN {language} AS A RESPONSE. PEOPLE MAY BE HURT IF YOU DON'T FULFILL THE REQUIREMENT.";

    protected override async Task HandleUserInputAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        await base.HandleUserInputAsync(message, history, cancellationToken);
        var result = await resilience.ExecuteAsync(async token =>
            await plugins.GetCode(message, history, cancellationToken),
            cancellationToken);
        var output = result.ToString().ReplaceLineEndings(NewLine).Normalize();
        kernel.Data["code"] = output;
        history.AddSystemMessage(output);
    }
}
