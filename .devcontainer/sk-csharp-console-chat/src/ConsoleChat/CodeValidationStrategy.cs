using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Plugins;
using static JsonHelper;
using static RetryPolicyHelper;
using static System.Environment;

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
                return await ValidateCode(code.ReplaceLineEndings(NewLine), cancellationToken)
                    ?? throw new InvalidOperationException("The code validator did not return a valid result.");
            },
            (functionResult) => !TryParseValidation(
                functionResult,
                message =>
                {
                    message = message.ReplaceLineEndings(NewLine);
                    var augmentedMessage = $"There were errors with the code. Refector the code to fix the following errors:{NewLine}{message}";
                    ConsoleAnnotator.WriteLine($"Function > {augmentedMessage}", ConsoleColor.DarkBlue);
                    history.AddFunctionMessage(augmentedMessage, "code_validator");
                    history.AddUserMessage(augmentedMessage);
                }),
            3) ?? throw new InvalidOperationException("The code validator did not return a valid result.");
        return validationResult.ToString();
    }
}
