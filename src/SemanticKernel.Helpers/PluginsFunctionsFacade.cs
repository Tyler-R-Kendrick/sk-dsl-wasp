using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Plugins;
using static System.Environment;

public class PluginsFunctionsFacade(Kernel kernel)
{
    public async Task<FunctionResult> LintCode(
        string linterPath,
        string code, string language = "csharp",
        CancellationToken cancellationToken = default)
        => await kernel.InvokeAsync("code_linter", "LintCode", new()
        {
            { "code", code },
            { "path", linterPath },
            { "language", language }
        }, cancellationToken);
    
    public async Task<FunctionResult> GetCode(string input, ChatHistory localHistory,
        CancellationToken cancellationToken, string grammar = "grammars/csharp.g4")
        => await kernel.InvokeAsync("yaml_plugins", "generateCode", new()
        {
            { "input", input },
            { "grammar", await File.ReadAllLinesAsync(grammar) },
            { "fewShotExamples", await File.ReadAllLinesAsync("examples/csharp.md") },
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