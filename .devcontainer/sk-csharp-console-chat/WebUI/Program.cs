using DslCopilot.WebUI.Components;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
using Plugins;

var builder = WebApplication.CreateBuilder(args);

// 1. Create a FunctionFilter and register that as a service
// 1.1. In the FunctionFilter, you need to call validation *only* when CodeGen runs
// 2. Inject a ResliencePipeline from Polly and register a retry policy

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

KernelSettings? kernelSettings = builder.Configuration.GetRequiredSection("SemanticKernel").Get<KernelSettings>();
if (kernelSettings is null)
{
    throw new InvalidOperationException("Invalid semantic kernel settings, please provide configuration settings using instructions in the README.");
}

builder.Services
  // Add kernel settings to the host builder
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


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
  app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
