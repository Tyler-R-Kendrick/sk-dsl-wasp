using Microsoft.SemanticKernel;
using Polly;

namespace Plugins
{
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
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}