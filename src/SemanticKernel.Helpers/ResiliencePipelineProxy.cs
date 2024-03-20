using Castle.DynamicProxy;
using Polly.Registry;

namespace Plugins;

public class ResilienceProxy<T>(ResiliencePipelineRegistry<T> pipelineRegistry) : IInterceptor
    where T : notnull
{
    public void Intercept(IInvocation invocation)
    {
        if (invocation.InvocationTarget is T target)
        {
            _ = pipelineRegistry.TryGetPipeline(target, out var pipeline);
            pipeline?.Execute(() => invocation.Proceed());
        }
        invocation.Proceed();
    }

}
public class ResilienceProxy
{
    public static TDecorate Decorate<TDecorate>(TDecorate target,
        ProxyGenerator generator,
        ResiliencePipelineRegistry<TDecorate> pipelineRegistry)
        where TDecorate : class =>
            generator.CreateClassProxyWithTarget<TDecorate>(target, new ResilienceProxy<TDecorate>(pipelineRegistry));
}