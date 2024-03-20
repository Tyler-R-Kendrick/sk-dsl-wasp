using Microsoft.SemanticKernel;

namespace Plugins;

internal sealed class DefaultKernelPlugin(string name, string? description, IEnumerable<KernelFunction>? functions = null)
    : KernelPlugin(name, description)
{
    private readonly Dictionary<string, KernelFunction> _functions
        = functions?.ToDictionary(x => x.Name, x => x)
        ?? new(StringComparer.OrdinalIgnoreCase);

    public override int FunctionCount => _functions.Count;

    public override bool TryGetFunction(string name, out KernelFunction function)
        => _functions.TryGetValue(name, out function!);

    public override IEnumerator<KernelFunction> GetEnumerator()
        => _functions.Values.GetEnumerator();
}
