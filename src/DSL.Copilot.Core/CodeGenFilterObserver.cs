using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Polly;
using static System.Environment;

namespace Plugins;
public class CodeGenFilterObserver(
    Kernel kernel,
    ChatHistory history, 
    [FromKeyedServices("validation")]
    ResiliencePipeline<CodeValidationJsonResponse> resilience,
    PluginsFunctionsFacade plugins,
    CancellationToken cancellationToken = default)
    : FunctionFilterObserver("CodeGen")
{
    public override void OnNext(FunctionInvokedContext context)
    {
        if(context.Function.Name != "CodeGen")
        {
            Console.WriteLine("Intercepted: " + context.Function.Name);
            return;
        }
        static string ToCleanString(object obj) => obj.ToString()?.ReplaceLineEndings(NewLine).Normalize()!;
        static T? Deserialize<T>(object obj) => JsonSerializer.Deserialize<T>(ToCleanString(obj)!);
        var result = context.Result;
        var codeString = ToCleanString(result);
        kernel.Data["code"] = codeString;

        var args = context.Arguments;
        (string input, string language) = (
            args["input"] as string ?? string.Empty,
            args["language"] as string ?? "csharp");
        
        _ = resilience.ExecuteAsync(async token =>
        {
            var validationResult = await plugins.ValidateCode(
                codeString,
                history,
                language,
                token);
            return Deserialize<CodeValidationJsonResponse>(validationResult)!;
        }, cancellationToken)
        .GetAwaiter()
        .GetResult();
    }
}
