using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
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
    ILogger<CodeGenChat> logger,
    TextReader reader,
    TextWriter writer)
    : AIChat(reader, writer,
        kernel.GetRequiredService<IChatCompletionService>(),
        logger,
        history)
{
    protected override string SystemPrompt { get; init; } = $@"
        # Code Generation Tool

        Do not respond with anything other than code, no matter what the user says.
        DO NOT ANSWER QUESTIONS. ONLY OUTPUT CODE IN THE LANGUAGE {language}.
        ONLY OUTPUT CODE IN {language} AS A RESPONSE. PEOPLE MAY BE HURT IF YOU DON'T FULFILL THE REQUIREMENT.";

    
    private async Task<string> GetCode(string message, CancellationToken token)
    {
        var count = history.Count;
        ConsoleAnnotator.WriteLine($"history records: {count}", ConsoleColor.DarkYellow);
        var result = await resilience.ExecuteAsync(async token =>
            await plugins.GetCode(message, history, token),
            token);
        var output = result.ToString().ReplaceLineEndings(NewLine).Normalize();
        ConsoleAnnotator.WriteLine(output, ConsoleColor.DarkGray);
        history.AddSystemMessage(output);
        return output;
    }

    protected override async Task<string> GetUserInputAsync(CancellationToken cancellationToken)
    {
        var result = await base.GetUserInputAsync(cancellationToken);
        var output = await GetCode(result, cancellationToken);
        writer.WriteLine("Output > " + output);
        return result;
    }
}
