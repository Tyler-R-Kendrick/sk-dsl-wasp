using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Polly.Registry;

namespace Plugins;

//
// Summary:
//     Provides an Microsoft.SemanticKernel.KernelPlugin implementation around a collection
//     of functions.
internal sealed class DefaultKernelPlugin : KernelPlugin
{
    //
    // Summary:
    //     The collection of functions associated with this plugin.
    private readonly Dictionary<string, KernelFunction> _functions;

    public override int FunctionCount => _functions.Count;
    internal DefaultKernelPlugin(string name, string? description, IEnumerable<KernelFunction>? functions = null)
        : base(name, description)
    {
        _functions = new Dictionary<string, KernelFunction>(StringComparer.OrdinalIgnoreCase);
        if (functions == null)
        {
            return;
        }

        foreach (KernelFunction function in functions)
        {
            //Verify.NotNull(function, "functions");
            _functions.Add(function.Name, function);
        }
    }

    public override bool TryGetFunction(string name, out KernelFunction function)
    {
        return _functions.TryGetValue(name, out function!);
    }

    public override IEnumerator<KernelFunction> GetEnumerator()
    {
        return _functions.Values.GetEnumerator();
    }
}
public static class KernelBuilderPluginsExtensions
{
    public static IKernelBuilderPlugins AddFromType<T>(this IKernelBuilderPlugins builder,
        string pluginName,
        Action<ResiliencePipelineRegistry<T>> policyFactory)
        where T : class
    {
        var registry = new ResiliencePipelineRegistry<T>();
        policyFactory(registry);
        //TODO: Implement without castle. Use DispatchProxy and IKernelPlugin.
        // Maybe with regular ServiceDescription injection instead.
        var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
        var instance = builder.Services.BuildServiceProvider().GetService<T>()!;
        var proxy = ResilienceProxy.Decorate(instance, proxyGenerator, registry);
        ConsoleAnnotator.WriteLine($"registered: {pluginName}", ConsoleColor.DarkGreen);
        return builder.AddFromObject(proxy, pluginName);
    }
    private static KernelFunction GetFunctionFromDirectory(string pluginName,
        DirectoryInfo directoryInfo, 
        ILogger logger, 
        ILoggerFactory loggerFactory, 
        IPromptTemplateFactory factory,
        ResiliencePipelineRegistry<KernelFunction> pipelineRegistry)
    {
        ConsoleAnnotator.WriteLine($"registering: {pluginName}.{directoryInfo.Name}", ConsoleColor.DarkGreen);
        string fileName = directoryInfo.FullName;
        string path = Path.Combine(fileName, "skprompt.txt");
        string path2 = Path.Combine(fileName, "config.json");
        PromptTemplateConfig promptTemplateConfig = File.Exists(path2)
            ? PromptTemplateConfig.FromJson(File.ReadAllText(path2))
            : new PromptTemplateConfig();
        promptTemplateConfig.Name = directoryInfo.Name;
        promptTemplateConfig.Template = File.Exists(path)
            ? File.ReadAllText(path)
            : throw new FileNotFoundException($"The prompt template file {path} was not found.");
        IPromptTemplate promptTemplate = factory.Create(promptTemplateConfig);
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Registering function {0}.{1} loaded from {2}", pluginName, fileName, directoryInfo);
        }
        ConsoleAnnotator.WriteLine($"registered: {pluginName}.{fileName}", ConsoleColor.DarkGreen);
        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(
            promptTemplate, promptTemplateConfig, loggerFactory)!;
        //TODO: Create proxy for resilience
        return ResilienceProxy.Decorate(
            function,
            new Castle.DynamicProxy.ProxyGenerator(),
            pipelineRegistry);
    }
    private static IEnumerable<KernelFunction> GetFunctionsFromDirectories(
        DirectoryInfo directoryInfo, ILoggerFactory loggerFactory,
        Action<KernelFunction, ResiliencePipelineRegistry<KernelFunction>> functionPolicyBuilder)
    {
        ConsoleAnnotator.WriteLine($"GetFunctionsFromDirectories: {directoryInfo}", ConsoleColor.DarkGreen);
        var directories = directoryInfo.GetDirectories();
        foreach (var pluginDirectory in directories)
        {
            ConsoleAnnotator.WriteLine($"plugin dir name: {pluginDirectory.Name}", ConsoleColor.DarkGreen);
            ConsoleAnnotator.WriteLine($"plugin dir fullname: {pluginDirectory.FullName}", ConsoleColor.DarkGreen);
            var pluginName = pluginDirectory.Name;
            IPromptTemplateFactory factory = new KernelPromptTemplateFactory(loggerFactory);
            var kernelPlugin = new DefaultKernelPlugin(pluginName, null, []);
            ILogger logger = loggerFactory.CreateLogger(typeof(Kernel));
            var functionResilienceRegistry = new ResiliencePipelineRegistry<KernelFunction>();
            ConsoleAnnotator.WriteLine($"plugin dir: {pluginDirectory}", ConsoleColor.DarkGreen);
            //TODO: Use and register the registry.
            var func = GetFunctionFromDirectory(pluginName, pluginDirectory,
                logger, loggerFactory, factory, functionResilienceRegistry);
            functionPolicyBuilder(func, functionResilienceRegistry);
            yield return func;
        }
    }

    public static IKernelBuilderPlugins AddFromPromptDirectory(this IKernelBuilderPlugins plugins,
        string directory,
        Action<KernelPlugin, ResiliencePipelineRegistry<KernelPlugin>> kernelPolicyBuilder,
        Action<KernelFunction, ResiliencePipelineRegistry<KernelFunction>> functionPolicyBuilder)
    {
        ConsoleAnnotator.WriteLine($"prompt dir: {directory}", ConsoleColor.DarkGreen);
        var services = plugins.Services.BuildServiceProvider();
        var directoryInfo = new DirectoryInfo(directory);
        var functions = GetFunctionsFromDirectories(directoryInfo,
            services.GetRequiredService<ILoggerFactory>(),
            functionPolicyBuilder);
        var plugin = new DefaultKernelPlugin(directory, null, functions);
        plugins.Add(plugin);
        kernelPolicyBuilder(plugin, new ResiliencePipelineRegistry<KernelPlugin>());
        return plugins;
    }

    public static IKernelBuilderPlugins ConfigureKernelPluginPolicy(this IKernelBuilderPlugins plugins,
        KernelPlugin plugin, Action<KernelPlugin, ResiliencePipelineRegistry<KernelPlugin>> policyBuilder)
    {
        var registry = new ResiliencePipelineRegistry<KernelPlugin>();
        policyBuilder(plugin, registry);
        var services = plugins.Services;
        var serviceDescriptor = services
            .Where(x => ((KernelPlugin?)x.ImplementationInstance) == plugin)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The plugin was not found in the service collection.");
        services.Remove(serviceDescriptor);
        var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
        var proxy = ResilienceProxy.Decorate((KernelPlugin)serviceDescriptor.ImplementationInstance!, proxyGenerator, registry);
        var newPlugin = new ServiceDescriptor(typeof(KernelPlugin), proxy);
        services.Add(newPlugin);
        return plugins;
    }
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
        
class FunctionFilter(Action<FunctionInvokedContext>? onInvoked, Action<FunctionInvokingContext>? onInvoking) : IFunctionFilter
{
    public void OnFunctionInvoked(FunctionInvokedContext context) => onInvoked?.Invoke(context);

    public void OnFunctionInvoking(FunctionInvokingContext context) => onInvoking?.Invoke(context);
}
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

}
