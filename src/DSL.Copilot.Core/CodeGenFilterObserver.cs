using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using static System.Environment;

namespace Plugins;
public class CodeGenFilterObserver(PluginsFunctionsFacade plugins, int maxRetryAttempts = 3)
    : FunctionFilterObserver("CodeGen")
{
    public override void OnNext(FunctionInvokedContext context)
    {
        OnNextAsync(context).GetAwaiter().GetResult();
        base.OnNext(context);
    }
    public async Task OnNextAsync(FunctionInvokedContext context)
    {
        static string ToCleanString(object obj) => obj.ToString()?.ReplaceLineEndings(NewLine).Normalize()!;
        static T? Deserialize<T>(object obj) => JsonSerializer.Deserialize<T>(ToCleanString(obj)!);
        int retryAttempt = 0;
        ConsoleAnnotator.WriteLine("Intercepting...", ConsoleColor.DarkGreen);
        var result = context.Result;
        var code = Deserialize<CodeGenJsonResponse>(result)?.Code ?? string.Empty;
        var codeString = ToCleanString(code);
        var args = context.Arguments;
        (string input, string history, string language, string grammar) = (
            args["input"] as string ?? string.Empty,
            args["history"] as string ?? string.Empty,
            args["language"] as string ?? "csharp",
            args["grammar"] as string ?? "grammars/csharp.g4");
        var validationResult = await plugins.ValidateCode(
            input,
            codeString,
            history,
            new CancellationToken(),
            language);
        var codeValidationResponse = Deserialize<CodeValidationJsonResponse>(validationResult);
        if(codeValidationResponse != null
            && codeValidationResponse.Errors != null
            && codeValidationResponse.Errors.Length != 0)
        {
            var errors = NewLine + string.Join(NewLine, codeValidationResponse.Errors);
            history += errors;
        }
        if(codeValidationResponse?.IsValid == false && retryAttempt++ < maxRetryAttempts)
        {
            ConsoleAnnotator.WriteLine($"Retrying {retryAttempt}", ConsoleColor.DarkGreen);
            result = await plugins.Retry(context, new()
            {
                { "history", history },
            });
        }
    }
}
