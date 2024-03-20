using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Plugins;

public static class KernelBuilderPluginsExtensions
{
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    public static IKernelBuilderPlugins AddFunctionFilter(
        this IKernelBuilderPlugins plugins,
        Action<FunctionInvokedContext>? onInvoked = null,
        Action<FunctionInvokingContext>? onInvoking = null)
        {
            var filter = new FunctionFilter(onInvoked, onInvoking);
            plugins.Services.AddSingleton<IFunctionFilter>(filter);
            return plugins;
        }
        
    private class FunctionFilter(
        Action<FunctionInvokedContext>? onInvoked,
        Action<FunctionInvokingContext>? onInvoking)
        : IFunctionFilter
    {
        public void OnFunctionInvoked(FunctionInvokedContext context) => onInvoked?.Invoke(context);

        public void OnFunctionInvoking(FunctionInvokingContext context) => onInvoking?.Invoke(context);
    }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

}
