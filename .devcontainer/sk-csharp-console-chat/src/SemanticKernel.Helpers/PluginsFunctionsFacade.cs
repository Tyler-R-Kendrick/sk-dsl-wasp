using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Plugins;
using static System.Environment;

public class PluginsFunctionsFacade(Kernel kernel)
{
    public Task<FunctionResult> GetCode(string input, ChatHistory localHistory,
        CancellationToken cancellationToken, string grammar = "grammars/csharp.g4", string language = "csharp")
        => kernel.InvokeAsync("plugins", "CodeGen", new()
        {
            { "input", input },
            { "grammar", grammar },
            { "language", language },
            { "history", string.Join(NewLine, localHistory) }
        }, cancellationToken);

    public Task<FunctionResult> ValidateCode(string input, string code,
        string? history,
        CancellationToken cancellationToken,
        string language = "csharp")
        => kernel.InvokeAsync("code_validator", "ValidateCode", new()
        {
            { "input", input },
            { "language", language },
            { "history", history },
            { "code", code }
        }, cancellationToken);

    public Task<FunctionResult> Retry(FunctionInvokedContext context, KernelArguments args)
    => kernel.InvokeAsync(context.Function, context.Arguments
        .Union(args, (x, y) => x.Key == y.Key, x => x.Key.GetHashCode())
        .ToKernelArguments());
}
