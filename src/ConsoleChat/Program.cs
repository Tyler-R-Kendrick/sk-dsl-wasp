using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Plugins;
using Polly;
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
    var functionFacade = new FunctionFilterMediator();
    services
        .AddSingleton(kernelSettings)
        .AddTransient(serviceProvider => {
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(c => c
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));
            builder.Services.AddChatCompletionService(kernelSettings);
            builder.Plugins.AddFromType<ConsoleLogPlugin>("console");
            builder.Plugins.AddFunctionFilter(onInvoked: context => 
            {
                ConsoleAnnotator.WriteLine($"func: {context.Function.Name}", ConsoleColor.DarkGreen);
                functionFacade.OnNext(context);
            });
            builder.Plugins.AddFromType<CodeValidatorPlugin>("code_validator");
            builder.Plugins.AddFromType<FileIOPlugin>("file");
            builder.Plugins.AddFromType<ConversationSummaryPlugin>();
            builder.Plugins.AddFromPromptDirectory("plugins");
            var kernel = builder.Build();
            KernelFunction formatAsJsonFunction = kernel.CreateFunctionFromPromptYaml(
                File.ReadAllText("plugins/formatAsJson.yaml")!,
                promptTemplateFactory: new HandlebarsPromptTemplateFactory()
            );
            builder.Plugins.AddFromFunctions("plugins", [formatAsJsonFunction]);
            return kernel;
        })
        .AddResilienceEnricher()
        .AddSingleton<PluginsFunctionsFacade>()
        .AddSingleton<CodeGenFilterObserver>()
        .AddSingleton<ChatHistory>([])
        .AddResiliencePipeline<string, CodeValidationJsonResponse>("validation", config =>
        {
            config.AddRetry(new()
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = args => ValueTask.FromResult(!args.Outcome!.Result!.IsValid),
                OnRetry = args => ValueTask.CompletedTask
            });
        })
        .AddHostedService<CodeGenChat>(provider =>
        {
            var kernel = provider.GetRequiredService<Kernel>();
            functionFacade.Subscribe(provider.GetRequiredService<CodeGenFilterObserver>());
            return new(
                kernel,
                new PluginsFunctionsFacade(kernel),
                provider.GetRequiredService<ChatHistory>(),
                File.ReadAllText("grammars/csharp.g4"),
                provider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<CodeGenChat>());
        });
});


// Build and run the host. This keeps the app running using the HostedService.
var host = builder.Build();
await host.RunAsync();