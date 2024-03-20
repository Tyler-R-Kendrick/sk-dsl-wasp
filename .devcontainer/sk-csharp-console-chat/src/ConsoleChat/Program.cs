using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
using Plugins;
// Load the kernel settings
var kernelSettings = KernelSettings.LoadSettings();

// Create the host builder with logging configured from the kernel settings.
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(kernelSettings.LogLevel ?? LogLevel.Warning);
    });

// Configure the services for the host
builder.ConfigureServices((_, services) =>
{
    // Add kernel settings to the host builder
    services
        .AddSingleton(kernelSettings)
        .AddTransient(serviceProvider => {
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(c => c
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));
            builder.Services.AddChatCompletionService(kernelSettings);
            builder.Services.AddSingleton<PluginsFunctionsFacade>();
            builder.Plugins.AddFromType<ConsoleLogPlugin>("console");
            var functionFacade = new FunctionFilterMediator();
            builder.Plugins.AddFunctionFilter(onInvoked: context => 
            {
                ConsoleAnnotator.WriteLine($"func: {context.Function.Name}", ConsoleColor.DarkGreen);
                functionFacade.OnNext(context);
            });
            builder.Plugins.AddFromType<CodeValidatorPlugin>("code_validator");
#pragma warning disable SKEXP0050 // Type or member is obsolete
            builder.Plugins.AddFromType<FileIOPlugin>("file");
            builder.Plugins.AddFromType<ConversationSummaryPlugin>();
#pragma warning restore SKEXP0050 // Type or member is obsolete
            builder.Plugins.AddFromPromptDirectory("plugins");
            var kernel = builder.Build();
            var plugins = new PluginsFunctionsFacade(kernel);
            functionFacade.Subscribe(new CodeGenFilterObserver(plugins));
            return kernel;
        })
        .AddResilienceEnricher()
        .AddSingleton<PluginsFunctionsFacade>()
        .AddHostedService<CodeGenChat>();
});

// Build and run the host. This keeps the app running using the HostedService.
var host = builder.Build();
await host.RunAsync();