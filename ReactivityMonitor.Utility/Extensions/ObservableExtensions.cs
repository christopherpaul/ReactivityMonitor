﻿using DynamicData;
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

        /// <summary>
        /// Only emits values from <paramref name="source"/> when the latest value from <paramref name="gate"/>
        /// is true. <paramref name="source"/> values that arrive at other times are held and emitted when
        /// <paramref name="gate"/> next turns true.
        /// </summary>
        /// <remarks>
        /// <para>Before <paramref name="gate"/> emits its first value, <paramref name="source"/> values are
        /// held.</para>
        /// <para>Completes when <paramref name="source"/> completes and its values have all 
        /// passed through the gate. If <paramref name="gate"/> completes, all remaining values
        /// are allowed to pass through.</para>
        /// </remarks>
        public static IObservable<T> Gate<T>(this IObservable<T> source, IObservable<bool> gate)
        {
            // DistinctUntilChanged is important, otherwise a false following a false would
            // release the values held by the first false.
            var whenIsUpdatingChanges = gate
                .StartWith(false)
                .Append(true)
                .DistinctUntilChanged();

            return source.Publish(sourceSafe =>
                whenIsUpdatingChanges.Publish(isUpdatingSafe =>
                    isUpdatingSafe
                        .Select(isUpdating =>
                            isUpdating
                                ? sourceSafe.TakeUntil(isUpdatingSafe)
                                : sourceSafe.Concat(Observable.Never<T>()).Buffer(isUpdatingSafe).Take(1).SelectMany(buf => buf))
                        .TakeUntil(sourceSafe.WhenTerminated())
                        .Concat()));
        }

        public static IObservable<T> ConnectForEver<T>(this IConnectableObservable<T> source)
        {
            source.Connect();
            return source;
        }
    }
}
