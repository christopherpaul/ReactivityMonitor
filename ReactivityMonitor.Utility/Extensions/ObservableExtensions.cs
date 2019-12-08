using DynamicData;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;

namespace ReactivityMonitor.Utility.Extensions
{
    public static class ObservableExtensions
    {
        public static IObservable<IChangeSet<T>> AsChangeSets<T>(this IObservable<T> source)
        {
            return source.Select(item => new Change<T>(ListChangeReason.Add, item).ToChangeSet());
        }

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
    }
}
