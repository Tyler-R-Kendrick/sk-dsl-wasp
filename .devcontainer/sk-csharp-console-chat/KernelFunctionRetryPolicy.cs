using Microsoft.SemanticKernel;
namespace Plugins;
internal class KernelFunctionRetryPolicy(
    KernelFunction function,
    Func<Task<KernelFunctionExecutionContext>, bool> retryCondition,
    int maxRetries = 3)
{
    public async Task<FunctionResult> InvokeAsync(
        Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken)
        => await RetryPolicyHelper.Invoke(
            () => function.InvokeAsync(kernel, arguments, cancellationToken),
            async result => new KernelFunctionExecutionContext(kernel, await result, arguments, cancellationToken),
            retryCondition,
            maxRetries);
}
