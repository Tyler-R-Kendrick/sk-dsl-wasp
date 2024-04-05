using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Plugins;
using static System.Environment;

public class PluginsFunctionsFacade(Kernel kernel)
{
    public async Task<FunctionResult> GetCode(string input, ChatHistory localHistory,
        CancellationToken cancellationToken, string grammar = "grammars/csharp.g4")
        => await kernel.InvokeAsync("yaml_plugins", "generateCode", new()
        {
            { "input", input },
            { "grammar", await File.ReadAllLinesAsync(grammar) },
            { "history", string.Join(NewLine, localHistory) }
        }, cancellationToken);

    public Task<FunctionResult> ValidateCode(string input,
        string language = "csharp",
        CancellationToken cancellationToken = default)
        => kernel.InvokeAsync("code_validator", "ValidateCode", new()
        {
            { "input", input },
            { "language", language }
        }, cancellationToken);

    public Task<FunctionResult> Retry(FunctionInvokedContext context, KernelArguments args)
    => kernel.InvokeAsync(context.Function, context.Arguments
        .Union(args, (x, y) => x.Key == y.Key, x => x.Key.GetHashCode())
        .ToKernelArguments());
}
