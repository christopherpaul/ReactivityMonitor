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

        public static IObservable<Unit> WhenTerminated<T>(this IObservable<T> source) => source.IgnoreElements().Select(Funcs<T>.DefaultOf<Unit>()).Append(default);

        /// <summary>
        /// Provides an observable (via <paramref name="whenSubscriptionCountChanges"/>) whose value is the number of active
        /// subscriptions made to <paramref name="source"/> via this operator.
        /// </summary>
        /// <returns>An observable equivalent to <paramref name="source"/>.</returns>
        public static IObservable<T> MonitorSubscriptionCount<T>(this IObservable<T> source, out IObservable<int> whenSubscriptionCountChanges)
        {
            var subCountChangeSubject = Subject.Synchronize(new Subject<int>());
            whenSubscriptionCountChanges = subCountChangeSubject.Scan(0, (count, delta) => count + delta).StartWith(0);

            return Observable.Create<T>(observer =>
            {
                subCountChangeSubject.OnNext(1);

                return StableCompositeDisposable.Create(
                    source.Subscribe(observer),
                    Disposable.Create(() => subCountChangeSubject.OnNext(-1)));
            });
        }
    }
}
