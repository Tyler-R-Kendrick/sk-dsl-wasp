using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Polly;

namespace Plugins;

public class DefaultAwaitableFunctionFilter(
    Action<FunctionInvokingContext> onInvoking,
    Action<FunctionInvokedContext> onInvoked,
    Func<Func<TaskAwaiter>, TaskAwaiter> onGetAwaiter)
    : AwaitableFunctionFilter
{
    public override void OnFunctionInvoking(FunctionInvokingContext context)
    {
        onInvoking(context);
        base.OnFunctionInvoking(context);
    }
    public override void OnFunctionInvoked(FunctionInvokedContext context)
    {
        onInvoked(context);
        base.OnFunctionInvoked(context);
    }
    public override TaskAwaiter GetAwaiter()
        => onGetAwaiter(base.GetAwaiter);
}
public static class FunctionFilterFactory
{
    public static IFunctionFilter Create(
        Action<FunctionInvokingContext> onInvoking,
        Action<FunctionInvokedContext> onInvoked,
        Func<Func<TaskAwaiter>, TaskAwaiter>? onGetAwaiter = null)
        => new DefaultAwaitableFunctionFilter(
            onInvoking, onInvoked, onGetAwaiter ?? (func => func()));
}
public class PollyFunctionFilter(ResiliencePipeline resilience,
    ResiliencePipeline<FunctionInvokingContext> invokingPipeline,
    ResiliencePipeline<FunctionInvokedContext> invokedPipeline)
    : AwaitableFunctionFilter
{
    public override void OnFunctionInvoking(FunctionInvokingContext context)
        => invokingPipeline.Execute(() =>
        {
            base.OnFunctionInvoking(context);
            return context;
        });

    public override void OnFunctionInvoked(FunctionInvokedContext context)
        => invokedPipeline.Execute(() =>
        {
            base.OnFunctionInvoked(context);
            return context;
        });

    public override TaskAwaiter GetAwaiter()
        => resilience.ExecuteAsync((token) =>
        {
            var awaiter = base.GetAwaiter();
            awaiter.GetResult();
            return ValueTask.CompletedTask;
        }).AsTask().GetAwaiter();
}

public static class FunctionFilterExtensions
{
    public static IFunctionFilter GetAwaiter(this IFunctionFilter filter)
        => new AwaitableFunctionFilterProxy(filter);
}

public class AwaitableFunctionFilterProxy(IFunctionFilter filter)
        : AwaitableFunctionFilter
{
    public override void OnFunctionInvoked(FunctionInvokedContext context)
    {
        filter.OnFunctionInvoked(context);
        base.OnFunctionInvoked(context);
    }
    public override void OnFunctionInvoking(FunctionInvokingContext context)
    {
        filter.OnFunctionInvoking(context);
        base.OnFunctionInvoking(context);
    }
    public override TaskAwaiter GetAwaiter() => base.GetAwaiter();
}
public class DefaultFunctionFilter(
    Action<FunctionInvokingContext>? onInvoking = null,
    Action<FunctionInvokedContext>? onInvoked = null)
    : IFunctionFilter
{
    public virtual void OnFunctionInvoked(FunctionInvokedContext context)
        => onInvoked?.Invoke(context);
    public virtual void OnFunctionInvoking(FunctionInvokingContext context)
        => onInvoking?.Invoke(context);
}

public class AwaitableFunctionFilter
    : IFunctionFilter
{
    readonly TaskCompletionSource<FunctionInvokingContext> functionInvoking = new();
    readonly TaskCompletionSource<FunctionInvokedContext> functionInvoked = new();
    readonly Lazy<Task> task;
    protected Task TaskHandle => task.Value;
    public AwaitableFunctionFilter()
        => task = new(() => Task.WhenAll([functionInvoked.Task, functionInvoking.Task]));

    public virtual void OnFunctionInvoked(FunctionInvokedContext context)
        => functionInvoked.SetResult(context);

    public virtual void OnFunctionInvoking(FunctionInvokingContext context)
        => functionInvoking.SetResult(context);

    public virtual TaskAwaiter GetAwaiter()
        => TaskHandle.GetAwaiter();
}
