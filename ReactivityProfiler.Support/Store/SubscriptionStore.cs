using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class SubscriptionStore : ISubscriptionStore
    {
        private readonly ConcurrentDictionary<long, SubscriptionInfo> mActiveSubscriptions =
            new ConcurrentDictionary<long, SubscriptionInfo>();

        public bool TraceEvents { get; set; } = true;

        public long CreateSub(ObservableInfo observable)
        {
            var sub = new SubscriptionInfo(observable);
            mActiveSubscriptions.TryAdd(sub.SubscriptionId, sub);

            if (TraceEvents)
            {
                System.Diagnostics.Trace.WriteLine($"Obs{observable.ObservableId}:Sub{sub.SubscriptionId}:Subscribe");
            }

            return sub.SubscriptionId;
        }

        public SubscriptionInfo GetSub(long subId)
        {
            return mActiveSubscriptions.TryGetValue(subId, out var sub) ? sub : null;
        }

        public IEnumerable<SubscriptionInfo> GetAllSubs()
        {
            return mActiveSubscriptions.Values;
        }

        public void DeleteSub(long subId)
        {
            if (TraceEvents)
            {
                var sub = GetSub(subId);
                System.Diagnostics.Trace.WriteLine($"Obs{sub?.Observable.ObservableId}:Sub{sub?.SubscriptionId}:Dispose");
            }

            mActiveSubscriptions.TryRemove(subId, out _);
        }
    }
}
