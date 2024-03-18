using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Polly;

namespace Plugins;

public static class ConsoleAnnotator
{
    public static string WriteLine(string message, ConsoleColor color = ConsoleColor.DarkBlue)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{message}]");
        Console.ResetColor();
        return message;
    }
}

public static class KernelBuilderPluginsExtensions
{
    public static IKernelBuilderPlugins AddFromObject(this IKernelBuilderPlugins builder,
        object target, string pluginName,
        Func<ResiliencePipelineBuilder, ResiliencePipeline> policyFactory)
    {
        var resiliencePipelineBuilder = new ResiliencePipelineBuilder();
        var pipeline = policyFactory(resiliencePipelineBuilder);
        var proxy = ResiliencePipelineProxy.Decorate(target, pipeline);
        return builder.AddFromObject(proxy, pluginName);
    }
    public static IKernelBuilderPlugins AddFromType<T>(this IKernelBuilderPlugins builder,
        string pluginName,
        Func<ResiliencePipelineBuilder, ResiliencePipeline> policyFactory)
        where T : notnull
    {
        var resiliencePipelineBuilder = new ResiliencePipelineBuilder();
        var pipeline = policyFactory(resiliencePipelineBuilder);
        var instance = builder.Services.BuildServiceProvider().GetRequiredService<T>();
        var proxy = ResiliencePipelineProxy.Decorate(Activator.CreateInstance<T>()!, pipeline);
        return builder.AddFromObject(proxy, pluginName);
    }
}

public class ResiliencePipelineProxy : DispatchProxy
{
    internal object? Target { get; private set; }
    internal ResiliencePipeline? Pipeline { get; private set; }

    public static object Decorate(object target, ResiliencePipeline pipeline)
    {
        var proxy = (ResiliencePipelineProxy)Create(target.GetType(), typeof(ResiliencePipelineProxy));
        proxy.Target = target;
        proxy.Pipeline = pipeline;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => Pipeline?.Execute(() => targetMethod?.Invoke(Target, args));
}
