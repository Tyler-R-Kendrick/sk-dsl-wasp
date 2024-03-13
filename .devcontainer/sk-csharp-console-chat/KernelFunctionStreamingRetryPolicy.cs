using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
namespace Plugins;

internal class KernelFunctionStreamingRetryPolicy(
    KernelFunction function,
    Func<KernelFunctionStreamingExecutionContext, bool> retryCondition,
    int maxRetries = 3)
{
    public IAsyncEnumerable<StreamingContentBase> InvokeStreamingAsync(
        Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken)
        => RetryPolicyHelper.Invoke(
            () => function.InvokeStreamingAsync(kernel, arguments, cancellationToken),
            result => new KernelFunctionStreamingExecutionContext(kernel, result, arguments, cancellationToken),
            retryCondition,
            maxRetries);
}
