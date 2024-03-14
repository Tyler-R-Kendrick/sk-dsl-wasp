using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Plugins;
using static JsonHelper;
using static RetryPolicyHelper;

internal class CodeGenerationStrategy(Kernel kernel)
{
    internal Task<FunctionResult> GetCode(string input, ChatHistory localHistory, CancellationToken cancellationToken)
        => kernel.InvokeAsync("plugins", "CodeGen", new()
        {
            { "input", input },
            { "grammar", "grammars/csharp.g4"},
            { "history", string.Join(Environment.NewLine, localHistory) }
        }, cancellationToken);

    internal async Task<string> ExecuteAsync(string userPrompt, ChatHistory history, CancellationToken cancellationToken)
    {
        var result = await RetryAsync(
            async (int attempt) =>
            {
                Console.WriteLine($"[attempting code gen: {attempt}]");
                var functionResult = await GetCode(userPrompt, history, cancellationToken);
                var output = functionResult.ToString();
                history.AddFunctionMessage(output, "code_gen");
                return functionResult;
            },
            (functionResult) => !TryParseGeneration(
                functionResult,
                message => history.AddFunctionMessage(message, "code_gen")),
            3);
        var resultString = (result?.ToString())
            ?? throw new InvalidOperationException("The code generator did not return a valid result.");
        var element = JsonSerializer.Deserialize<JsonElement?>(resultString);
        var code = element?.GetProperty("code").ToString()?.ReplaceLineEndings("");
        return code ?? throw new InvalidOperationException("The code generator did not return a valid code.");
    }
}
