using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Plugins;
using Polly;
using static System.Environment;
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
        .AddSingleton<ChatHistory>([])
        .AddResiliencePipeline<string, FunctionResult>("code-gen", (config, context) =>
        {
            config.AddRetry(new()
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = async args =>
                {
                    var services = context.ServiceProvider;
                    var history = services.GetRequiredService<ChatHistory>();
                    var codeOutcome = args.Outcome;
                    if(codeOutcome.Exception != null)
                    {
                        history.AddSystemMessage(codeOutcome.Exception.ToString());
                        return true;
                    }
                    var plugins = services.GetRequiredService<PluginsFunctionsFacade>();
                    var validationResult = await plugins.ValidateCode(
                        args.Outcome.Result!.ToString()!,
                        services.GetRequiredService<ChatHistory>(),
                        "csharp");
                    var resultString = validationResult.ToString().Normalize();
                    ConsoleAnnotator.WriteLine(resultString);
                    history.AddSystemMessage(resultString);
                    var jsonResult = JsonSerializer.Deserialize<CodeValidationJsonResponse>(resultString);
                    var isValid = jsonResult?.IsValid == true;
                    if(!isValid && jsonResult != null && jsonResult.Errors != null && jsonResult.Errors.Length != 0
                        && codeOutcome.Result != null && codeOutcome.Result.Metadata != null)
                    {
                        var codeResult = codeOutcome.Result;
                        var code = codeResult!.Metadata!["code"];
                        var codeString = $"The following code has errors:{NewLine}{code}";
                        var errors = string.Join(NewLine, jsonResult);
                        var errorString = $"Correct the following errors in the code:{NewLine}{errors}";
                        history.AddUserMessage(codeString + NewLine + errorString);
                    }
                    return !isValid;
                }
            });
        })
        .AddHostedService<CodeGenChat>(provider =>
        {
            var kernel = provider.GetRequiredService<Kernel>();
            return new(
                kernel,
                provider.GetRequiredKeyedService<ResiliencePipeline<FunctionResult>>("code-gen"),
                new PluginsFunctionsFacade(kernel),
                provider.GetRequiredService<ChatHistory>(),
                File.ReadAllText("grammars/csharp.g4"),
                "csharp",
                provider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<CodeGenChat>());
        });
});


// Build and run the host. This keeps the app running using the HostedService.
var host = builder.Build();
await host.RunAsync();