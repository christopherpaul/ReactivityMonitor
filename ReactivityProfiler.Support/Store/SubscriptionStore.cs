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
        private readonly ConcurrentDictionary<int, InstrumentationPointSubscriptions> mInstrumentationPointSubs =
            new ConcurrentDictionary<int, InstrumentationPointSubscriptions>();
        private readonly Func<int, InstrumentationPointSubscriptions> mIpsFactory = _ => new InstrumentationPointSubscriptions();

        public bool TraceEvents { get; set; } = false;

        public IStoreEventSink EventSink { get; set; }

        public long CreateSub(ObservableInfo observable)
        {
            var sub = new SubscriptionInfo(observable);
            mActiveSubscriptions.TryAdd(sub.SubscriptionId, sub);
            GetInstrumentationPointSubscriptions(observable.InstrumentationPoint).AddSub(sub);

            EventSink.Subscribed(sub);

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
            var details = CommonEventDetails.Capture();
            if (mActiveSubscriptions.TryRemove(subId, out var sub))
            {
                GetInstrumentationPointSubscriptions(sub.Observable.InstrumentationPoint).RemoveSub(sub);

                EventSink.Unsubscribed(ref details, sub);

                if (TraceEvents)
                {
                    System.Diagnostics.Trace.WriteLine($"Obs{sub?.Observable.ObservableId}:Sub{sub?.SubscriptionId}:Dispose");
                }
            }
        }

        public IEnumerable<SubscriptionInfo> GetSubs(int instrumentationPointId)
        {
            return GetInstrumentationPointSubscriptions(instrumentationPointId).ActiveSubs;
        }

        private InstrumentationPointSubscriptions GetInstrumentationPointSubscriptions(int instrumentationPointId)
        {
            return mInstrumentationPointSubs.GetOrAdd(instrumentationPointId, mIpsFactory);
        }

        private sealed class InstrumentationPointSubscriptions
        {
            private readonly ConcurrentDictionary<long, SubscriptionInfo> mActiveSubs =
                new ConcurrentDictionary<long, SubscriptionInfo>();

            public void AddSub(SubscriptionInfo sub)
            {
                mActiveSubs.TryAdd(sub.SubscriptionId, sub);
            }

            public void RemoveSub(SubscriptionInfo sub)
            {
                mActiveSubs.TryRemove(sub.SubscriptionId, out _);
            }

            public IEnumerable<SubscriptionInfo> ActiveSubs => mActiveSubs.Values;
        }
    }
}
