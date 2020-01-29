using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class RxEventStore : IRxEventStore
    {
        private readonly ISubscriptionStore mSubStore;

        public RxEventStore(ISubscriptionStore subStore)
        {
            mSubStore = subStore;
        }

        public IStoreEventSink EventSink { get; set; }

        public bool TraceEvents { get; } = false;

        public void AddOnNext<T>(SubscriptionInfo sub, T value)
        {
            if (sub == null)
            {
                return;
            }

            var details = CommonEventDetails.Capture();
            EventSink.OnNext(ref details, sub, value);

            if (TraceEvents)
            {
                Trace(sub.SubscriptionId, $"OnNext({value})");
            }
        }

        public void AddOnCompleted(SubscriptionInfo sub)
        {
            if (sub == null)
            {
                return;
            }

            var details = CommonEventDetails.Capture();
            EventSink.OnCompleted(ref details, sub);

            if (TraceEvents)
            {
                Trace(sub.SubscriptionId, "OnCompleted");
            }
        }

        public void AddOnError(SubscriptionInfo sub, Exception e)
        {
            if (sub == null)
            {
                return;
            }

            var details = CommonEventDetails.Capture();
            EventSink.OnError(ref details, sub, e);

            if (TraceEvents)
            {
                Trace(sub.SubscriptionId, $"OnError({e.Message})");
            }
        }

        private void Trace(long subId, string message)
        {
            var sub = mSubStore.GetSub(subId);
            System.Diagnostics.Trace.WriteLine($"Obs{sub?.Observable.ObservableId}:Sub{subId}:{message}");
        }
    }
}
