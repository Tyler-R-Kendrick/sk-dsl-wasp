using Microsoft.SemanticKernel;
namespace Plugins;
internal class KernelFunctionRetryPolicy(
    KernelFunction function,
    Func<KernelFunctionExecutionContext, bool> retryCondition,
    int maxRetries = 3)
{
    public async Task<FunctionResult?> InvokeAsync(
        Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken)
        => await RetryPolicyHelper.RetryAsync(
            (attempt) => function.InvokeAsync(kernel, arguments, cancellationToken),
            result => retryCondition(new KernelFunctionExecutionContext(kernel, result, arguments, cancellationToken)),
            maxRetries);
}
