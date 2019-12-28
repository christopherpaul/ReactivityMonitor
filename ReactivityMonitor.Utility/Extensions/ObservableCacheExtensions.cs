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

            return left.Group(leftItemKeySelector)
                .InnerJoin(rightKeys, k => k, (l, r) => l)
                .Transform(group =>
                {
                    var whenGroupRemoved = new Subject<Unit>();

                    var removeGroupItemsChangeSet = Observable.Defer(() =>
                        Observable.Return(new ChangeSet<TLeft, TLeftKey>(
                            group.Cache.KeyValues.Select(kvp => new Change<TLeft, TLeftKey>(ChangeReason.Remove, kvp.Key, kvp.Value)))));

                    var cacheWithRemovals =
                        group.Cache.Connect()
                            .TakeUntil(whenGroupRemoved)
                            .Concat(removeGroupItemsChangeSet);

                    return new { cacheWithRemovals, whenGroupRemoved };
                })
                .OnItemRemoved(g => g.whenGroupRemoved.OnNext(default))
                .MergeMany(g => g.cacheWithRemovals);
        }
    }
}
