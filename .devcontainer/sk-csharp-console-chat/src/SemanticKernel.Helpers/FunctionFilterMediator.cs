using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Microsoft.SemanticKernel;
using static System.Environment;

namespace Plugins
{
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
                ConsoleAnnotator.WriteLine($"observing {value.Function.Name}: {NewLine}{value.Result}", ConsoleColor.DarkGreen);
                switch(observer)
                {
                    case FunctionFilterObserver filterObserver:
                        if(filterObserver.Name == value.Function.Name)
                            filterObserver.OnNext(value);
                        break;
                    default:
                        observer.OnNext(value);
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