using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Plugins;
using static JsonHelper;
using static RetryPolicyHelper;

internal class CodeValidationStrategy(Kernel kernel,
    CodeGenerationStrategy codeGenerationStrategy)
{
    internal Task<FunctionResult> ValidateCode(string input, CancellationToken cancellationToken)
        => kernel.InvokeAsync("code_validator", "ValidateCode", new()
        {
            { "input", input },
            { "language", "csharp" }
        }, cancellationToken);

    internal async Task<string> ExecuteAsync(
        string userPrompt,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        var validationResult = await RetryAsync(
            async (int attempt) =>
            {
                Console.WriteLine($"[attempting code validation: {attempt}]");
                var code = await codeGenerationStrategy.ExecuteAsync(userPrompt, history, cancellationToken);
                var functionResult = await ValidateCode(code.ReplaceLineEndings(""), cancellationToken)
                    ?? throw new InvalidOperationException("The code validator did not return a valid result.");;
                history.AddFunctionMessage(functionResult.ToString(), "code_validator");
                return functionResult;
            },
            (functionResult) => !TryParseValidation(
                functionResult,
                message => history.AddFunctionMessage(message, "code_validator")),
            3) ?? throw new InvalidOperationException("The code validator did not return a valid result.");
        return validationResult.ToString();
    }
}
