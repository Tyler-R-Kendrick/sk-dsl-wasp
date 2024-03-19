using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Plugins;
using static System.Environment;

internal class CodeGenerationStrategy(Kernel kernel)
{
    internal Task<FunctionResult> GetCode(string input, ChatHistory localHistory, CancellationToken cancellationToken)
        => kernel.InvokeAsync("plugins", "CodeGen", new()
        {
            { "input", input },
            { "grammar", "grammars/csharp.g4"},
            { "language", "csharp"},
            { "history", string.Join(NewLine, localHistory) }
        }, cancellationToken);

    internal async Task<string> ExecuteAsync(string userPrompt, ChatHistory history, CancellationToken cancellationToken)
    {
        var functionResult = await GetCode(userPrompt, history, cancellationToken);
        var output = functionResult.ToString().ReplaceLineEndings(NewLine).Normalize();
        history.AddSystemMessage(output);
        ConsoleAnnotator.WriteLine($"code_gen:{NewLine}{output}{NewLine}", ConsoleColor.DarkBlue);
        return output;
    }
}
