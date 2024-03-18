using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Plugins;
using static JsonHelper;
using static RetryPolicyHelper;
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
        var maxAttempts = 3;
        var result = await RetryAsync(
            async (int attempt) =>
            {
                Console.WriteLine($"[attempting code gen: {attempt}]");
                var functionResult = await GetCode(userPrompt, history, cancellationToken);
                var output = functionResult.ToString().ReplaceLineEndings(NewLine);
                history.AddFunctionMessage(output, "code_gen");
                ConsoleAnnotator.WriteLine($"code_gen:{NewLine}{output}{NewLine}", ConsoleColor.DarkBlue);
                return functionResult;
            },
            (functionResult) => !TryParseGeneration(
                functionResult,
                message =>
                {
                    message = message.ReplaceLineEndings(NewLine);
                    var augmentedMessage = $"There were errors with the code generation. Fix the following errors:{NewLine}{message}";
                    ConsoleAnnotator.WriteLine($"Function > {augmentedMessage}", ConsoleColor.DarkBlue);
                    history.AddFunctionMessage(augmentedMessage, "code_gen");
                }),
            maxAttempts);
        var resultString = result?.ToString()
            ?? throw new InvalidOperationException("The code generator did not return a valid result.");
        var element = JsonSerializer.Deserialize<JsonElement?>(resultString);
        var code = element?.GetProperty("code").ToString()?.ReplaceLineEndings(NewLine);
        return code ?? throw new InvalidOperationException("The code generator did not return a valid code.");
    }
}
