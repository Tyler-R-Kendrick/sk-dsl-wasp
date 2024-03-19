using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
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
    services
        .AddSingleton(kernelSettings)
        .AddTransient(serviceProvider => {
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(c => c
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));
            builder.Services.AddChatCompletionService(kernelSettings);
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
            functionFacade.Subscribe(new CodeGenFilterObserver(kernel));
            return kernel;
        })
        .AddResilienceEnricher()
        .AddSingleton<CodeGenerationStrategy>()
        .AddHostedService<CodeGenChat>();
});

// Build and run the host. This keeps the app running using the HostedService.
var host = builder.Build();
await host.RunAsync();

namespace Plugins
{
    public record CodeGenJsonResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
    public record CodeValidationJsonResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("errors")] string[] Errors);
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    
    public class CodeGenFilterObserver(Kernel kernel, int maxRetryAttempts = 3)
        : FunctionFilterObserver("CodeGen")
    {
        public override void OnNext(FunctionInvokedContext context)
        {
            OnNextAsync(context).GetAwaiter().GetResult();
            base.OnNext(context);
        }
        public async Task OnNextAsync(FunctionInvokedContext context)
        {
            int retryAttempt = 0;
            ConsoleAnnotator.WriteLine("Intercepting...", ConsoleColor.DarkGreen);
            var result = context.Result;
            ConsoleAnnotator.WriteLine($"jsonResult:{NewLine}{result}", ConsoleColor.DarkGreen);
            var codeGenResponse = JsonSerializer.Deserialize<CodeGenJsonResponse>(result.ToString());
            var code = codeGenResponse?.Code.ToString().ReplaceLineEndings(NewLine).Normalize();
            ConsoleAnnotator.WriteLine($"Code:{NewLine}{code}", ConsoleColor.DarkGreen);
            var validationResult = await kernel.InvokeAsync("code_validator", "ValidateCode", new()
            {
                { "input", context.Arguments["input"] },
                { "language", context.Arguments["language"] },
                { "history", context.Arguments["history"] },
                { "code", code }
            });
            var codeValidationResponse = JsonSerializer.Deserialize<CodeValidationJsonResponse>(validationResult.ToString());
            var history = context.Arguments["history"] as string;
            if(history != null 
                && codeValidationResponse != null
                && codeValidationResponse.Errors != null
                && codeValidationResponse.Errors.Length != 0)
            {
                var errors = NewLine + string.Join(NewLine, codeValidationResponse.Errors);
                history += errors;
            }
            if(codeValidationResponse?.IsValid == false && retryAttempt++ < maxRetryAttempts)
            {
                ConsoleAnnotator.WriteLine($"Retrying {retryAttempt}", ConsoleColor.DarkGreen);
                result = await kernel.InvokeAsync(context.Function, new()
                {
                    { "input", context.Arguments["input"] },
                    { "history", history },
                    { "language", context.Arguments["language"] },
                    { "grammar", context.Arguments["grammar"] }
                });
            }
        }
    }

    public class FunctionFilterObserver(string name,
        Action<FunctionInvokedContext>? onNext = null,
        Action<Exception>? onError = null,
        Action? onCompleted = null,
        ResiliencePipeline<FunctionInvokedContext>? resiliencePipeline = null)
        : IObserver<FunctionInvokedContext>
    {
        public string Name = name;
        public virtual void OnCompleted() => onCompleted?.Invoke();
        public virtual void OnError(Exception error) => onError?.Invoke(error);
        public virtual void OnNext(FunctionInvokedContext value)
        {
            if(resiliencePipeline != null)
            {
                var result = resiliencePipeline.Execute(token =>
                {
                    onNext?.Invoke(value);
                    return value;
                }, new CancellationToken());
            }
            else
            {
                onNext?.Invoke(value);
            }
        }
    }
    
    public class FunctionFilterMediator
        : SubjectBase<FunctionInvokedContext>
    {
        private readonly List<IObserver<FunctionInvokedContext>> _observers = [];
        public override bool HasObservers => _observers.Count > 0;

        private bool _isDisposed = false;
        public override bool IsDisposed => _isDisposed;

        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _observers.Clear();
            GC.SuppressFinalize(this);
            _isDisposed = true;
        }

        public override void OnCompleted()
        {
            _observers.ForEach(observer => observer.OnCompleted());
        }

        public override void OnError(Exception error)
        {
            _observers.ForEach(observer => observer.OnError(error));
        }

        public override void OnNext(FunctionInvokedContext value)
        {            
            _observers.ForEach(observer =>
            {
                ConsoleAnnotator.WriteLine($"observing: {value.Function.Name}", ConsoleColor.DarkGreen);
                switch(observer)
                {
                    case FunctionFilterObserver filterObserver when filterObserver.Name == value.Function.Name:
                        ConsoleAnnotator.WriteLine("filtering observer");
                        filterObserver.OnNext(value);
                        break;
                    default:
                        //observer.OnNext(value);
                        break;
                }
            });
        }

        public override IDisposable Subscribe(IObserver<FunctionInvokedContext> observer)
        {
            _observers.Add(observer);
            return Disposable.Create(() => _observers.Remove(observer));
        }
    }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}