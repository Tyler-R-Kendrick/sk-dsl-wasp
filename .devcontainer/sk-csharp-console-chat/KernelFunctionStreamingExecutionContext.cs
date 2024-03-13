using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
namespace Plugins;

internal record KernelFunctionStreamingExecutionContext(
    Kernel Kernel,
    IAsyncEnumerable<StreamingContentBase> Result,
    KernelArguments Arguments,
    CancellationToken CancellationToken);
