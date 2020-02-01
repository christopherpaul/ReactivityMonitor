using DynamicData;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Text;
using System.Reactive.Subjects;
using System.Reactive;

namespace ReactivityMonitor.Utility.Extensions
{
    public static class ObservableCacheExtensions
    {
        public static IObservable<IChangeSet<TLeft, TLeftKey>> SemiJoinOnRightKey<TLeft, TLeftKey, TRight, TRightKey>(
            this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TLeft, TRightKey> leftItemKeySelector)
        {
            var rightKeys = right.Transform((_, k) => k);

            object workaroundInnerJoinLocker = new object();

            return left.Group(leftItemKeySelector).Synchronize(workaroundInnerJoinLocker)
                .InnerJoin(rightKeys.Synchronize(workaroundInnerJoinLocker), k => k, (l, r) => l)
                .SynchronizeSubscribe(workaroundInnerJoinLocker)
                .Transform(group =>
                {
                    var whenGroupRemoved = new Subject<Unit>();

                    var removeGroupItemsChangeSet = Observable.Defer(() =>
                        Observable.Return(new ChangeSet<TLeft, TLeftKey>(
                            group.Cache.KeyValues.Select(kvp => new Change<TLeft, TLeftKey>(ChangeReason.Remove, kvp.Key, kvp.Value)))));

                    var cacheWithRemovals =
                        group.Cache.Connect()
                            .Concat(Observable.Never<IChangeSet<TLeft, TLeftKey>>()) // avoid removing group items when left source terminates
                            .TakeUntil(whenGroupRemoved)
                            .Concat(removeGroupItemsChangeSet);

                    return new { cacheWithRemovals, whenGroupRemoved };
                })
                .OnItemRemoved(g => g.whenGroupRemoved.OnNext(default))
                .MergeMany(g => g.cacheWithRemovals);
        }

        /// <summary>
        /// Call DynamicData Switch method with workaround for concurrency bug
        /// </summary>
        public static IObservable<IChangeSet<TObject, TKey>> SwitchFixed<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            return Observable.Defer(() =>
            {
                object extraLocker = new object();
                var synchronisedSources = sources
                    .Select(s => s.Synchronize(extraLocker))
                    .Synchronize(extraLocker);

                return ObservableCacheEx.Switch(synchronisedSources)
                    .SynchronizeSubscribe(extraLocker);
            });
        }
    }
}
