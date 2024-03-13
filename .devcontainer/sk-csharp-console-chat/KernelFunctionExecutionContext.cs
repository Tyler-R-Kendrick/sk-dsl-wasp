using Microsoft.SemanticKernel;
namespace Plugins;

internal record KernelFunctionExecutionContext(
    Kernel Kernel,
    FunctionResult Result,
    KernelArguments Arguments,
    CancellationToken CancellationToken);
