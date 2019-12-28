using DynamicData;
using ReactivityMonitor.Utility.Flyweights;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace ReactivityMonitor.Utility.Extensions
{
    public static class ObservableExtensions
    {
        public static IObservable<T> Trace<T>(this IObservable<T> observable, string name)
        {
            int subSource = 0;
            return Observable.Create<T>(observer =>
            {
                int sub = Interlocked.Increment(ref subSource);
                Write("Subscribe");

                return observable
                    .Materialize()
                    .Do(WriteEvent)
                    .Dematerialize()
                    .Finally(() => Write("Unsubscribe"))
                    .Subscribe(observer);

                void WriteEvent(Notification<T> e) => Write($"{e.Kind}({(e.HasValue ? (object)e.Value : e.Exception?.Message)})");
                void Write(string s) => System.Diagnostics.Trace.WriteLine($"{name}({sub}): {s}");
            });
        }

        private static readonly Action<Subject<Unit>> cPushValueToSubject = s => s.OnNext(default);

        public static IObservable<T> TakeUntilDisposed<T>(this IObservable<T> source, CompositeDisposable disposables)
        {
            var whenDisposed = new Subject<Unit>();
            disposables.Add(Disposable.Create(whenDisposed, cPushValueToSubject));
            return source.TakeUntil(whenDisposed);
        }

        public static IObservable<Unit> WhenTerminated<T>(this IObservable<T> source) => source.IgnoreElements().Select(Funcs<Unit>.Default<T>()).Append(default);
    }
}
