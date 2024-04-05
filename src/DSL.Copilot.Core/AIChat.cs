using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Plugins;

public static class ObservableExtensions
{
    public static IObservable<T> FromTryCatch<T>(Func<Task<T>> func)
        => Observable.Create<T>(async observer =>
        {
            try
            {
                observer.OnNext(await func());
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
            finally
            {
                observer.OnCompleted();
            }
            return Disposable.Empty;
        });
}

public abstract partial class AIChat(
    TextReader reader,
    TextWriter writer,
    IChatCompletionService completions,
    ILogger<AIChat> logger,
    ChatHistory history)
    : BackgroundService
{
    protected abstract string SystemPrompt { init; get; }

    protected override sealed Task ExecuteAsync(CancellationToken token)
    {
        logger.LogTrace("Starting Chat Session...");
        history.Clear();
        history.AddSystemMessage(SystemPrompt);
        var observable = ObservableExtensions.FromTryCatch(async () =>
        {
            await writer.WriteAsync("User > ");
            var userInput = await GetUserInputAsync(token);

            return Unit.Default;
        }).DoWhile(() => !token.IsCancellationRequested);
        return observable.ToTask(token);
    }

    protected virtual async Task<string> GetUserInputAsync(CancellationToken token)
    {
        var result = await reader.ReadLineAsync()
                .ContinueWith(t => t.Result ?? string.Empty, token);
        history.AddUserMessage(result);
        return result;
    }
}