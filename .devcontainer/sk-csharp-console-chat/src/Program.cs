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
                KernelBuilder builder = new();
                builder.Services.AddLogging(c => c
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information));
                builder.Services.AddChatCompletionService(kernelSettings);
                builder.Plugins.AddFromType<ConsoleLogPlugin>("console");
                builder.Plugins.AddFromType<CodeValidatorPlugin>("code_validator");
    #pragma warning disable SKEXP0050 // Type or member is obsolete
                builder.Plugins.AddFromType<FileIOPlugin>("file");
                builder.Plugins.AddFromType<ConversationSummaryPlugin>();
    #pragma warning restore SKEXP0050 // Type or member is obsolete
                builder.Plugins.AddFromPromptDirectory("plugins");
                return builder.Build();
            })
            .AddHostedService<CodeGenChat>();
});

// Build and run the host. This keeps the app running using the HostedService.
var host = builder.Build();
await host.RunAsync();
