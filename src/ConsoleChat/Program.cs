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
    services
        .AddSingleton(kernelSettings)
        .AddTransient(serviceProvider =>
        {
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(c => c
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));
            builder.Services.AddChatCompletionService(kernelSettings);
            builder.Plugins.AddFromType<ConsoleLogPlugin>("console");
            builder.Plugins.AddFromType<CodeValidatorPlugin>("code_validator");
            builder.Plugins.AddFromType<CodeLinterPlugin>("code_linter");
            builder.Plugins.AddFromType<FileIOPlugin>("file");
            builder.Plugins.AddFromType<ConversationSummaryPlugin>();
            var kernel = builder.Build();
            kernel.FunctionFilters.Add(new DefaultFunctionFilter(
                onInvoked: async context =>
                {
                    ConsoleAnnotator.WriteLine($"intercepting {context.Function.Name}", ConsoleColor.DarkYellow);
                    if(context.Function.Name != "generateCode") return;
                    var provider = kernel.Services;
                    var result = await kernel.InvokeAsync("code_linter", "LintCode", new()
                    {
                        { "code", context.Result },
                        { "path", "config/.editorconfig" },
                        { "language", "csharp" }
                    });
                    context.SetResultValue(result.ToString());
                }));
            kernel.Plugins.AddFromFunctions("yaml_plugins", [
                kernel.CreateFunctionFromPromptYaml(
                    File.ReadAllText("plugins/formatAsJson.yaml")!,
                    promptTemplateFactory: new HandlebarsPromptTemplateFactory()),
                kernel.CreateFunctionFromPromptYaml(
                    File.ReadAllText("plugins/generateCode.yaml")!,
                    promptTemplateFactory: new HandlebarsPromptTemplateFactory()),
            ]);
            kernel.PromptFilters.Add(new DefaultPromptFilter(
                onRendering: context =>
                {
                    if(context.Arguments.ContainsName("input"))
                    {
                        ConsoleAnnotator.WriteLine("intercepting prompt rendering", ConsoleColor.DarkYellow);
                        var input = context.Arguments["input"] as string ?? string.Empty;
                        //Protects prompt injection from Handlebars
                        input = input
                            .Replace("{{", "{{{{")
                            .Replace("}}", "}}}}");
                        context.Arguments["input"] = input;
                    }
                },
                onRendered: context =>
                {
                    ConsoleAnnotator.WriteLine("intercepting prompt rendered", ConsoleColor.DarkYellow);
                    //Protects prompt injection from Handlebars
                    var prompt = context.RenderedPrompt
                            .Replace("{{", "{{{{")
                            .Replace("}}", "}}}}");
                    context.RenderedPrompt = prompt;
                }));
            return kernel;
        })
        .AddResilienceEnricher()
        .AddSingleton<PluginsFunctionsFacade>()
        .AddSingleton<ChatHistory>([])
        .AddSingleton<CodeValidationStrategy>()
        .AddResiliencePipeline<string, FunctionResult>("code-gen", (config, context) =>
        {
            config.AddRetry(new()
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = async args =>
                {
                    var services = context.ServiceProvider;
                    var codeOutcome = args.Outcome;
                    var exception = codeOutcome.Exception;
                    if (exception != null)
                    {
                        ConsoleAnnotator.WriteLine(exception.ToString(), ConsoleColor.DarkGray);
                        services
                            .GetRequiredService<ChatHistory>()
                            .AddSystemMessage(exception.ToString());
                        return true;
                    }
                    var shouldRetry = !await services
                        .GetRequiredService<CodeValidationStrategy>()
                        .ValidateAsync(codeOutcome.Result!);
                    ConsoleAnnotator.WriteLine($"Should retry: {shouldRetry}");
                    return shouldRetry;
                }
            });
        })
        .AddHostedService<CodeGenChat>(provider =>
        {
            T Get<T>(string? key = null) where T : notnull => key == null
                ? provider.GetRequiredService<T>()
                : provider.GetRequiredKeyedService<T>(key);
            return new(
                Get<Kernel>(),
                Get<ResiliencePipeline<FunctionResult>>("code-gen"),
                Get<PluginsFunctionsFacade>(),
                Get<ChatHistory>(),
                File.ReadAllText("grammars/csharp.g4"),
                "csharp",
                Get<ILoggerFactory>()
                    .CreateLogger<CodeGenChat>(),
                Console.In,
                Console.Out);
        });
});


// Build and run the host. This keeps the app running using the HostedService.
var host = builder.Build();
await host.RunAsync();