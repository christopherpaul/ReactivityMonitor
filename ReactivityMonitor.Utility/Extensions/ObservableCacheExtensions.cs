using DynamicData;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Text;

namespace ReactivityMonitor.Utility.Extensions
{
    public static class ObservableCacheExtensions
    {
        public static IObservable<IChangeSet<TLeft, TLeftKey>> SemiJoinOnRightKey<TLeft, TLeftKey, TRight, TRightKey>(
            this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TLeft, TRightKey> leftItemKeySelector)
        {
            return right.InnerJoinMany(left, leftItemKeySelector, (rightItem, leftGroup) => leftGroup)
                .TransformMany(leftGroup => leftGroup.KeyValues, kvp => kvp.Key)
                .Transform(kvp => kvp.Value);
        }
    }
}
