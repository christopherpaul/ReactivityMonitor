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

        public void AddOnNext<T>(long subscriptionId, T value)
        {
            var details = CommonEventDetails.Capture();
            var sub = mSubStore.GetSub(subscriptionId);
            if (sub == null)
            {
                return;
            }
            EventSink.OnNext(ref details, sub, value);

            if (TraceEvents)
            {
                Trace(subscriptionId, $"OnNext({value})");
            }
        }

        public void AddOnCompleted(long subscriptionId)
        {
            var details = CommonEventDetails.Capture();
            var sub = mSubStore.GetSub(subscriptionId);
            if (sub == null)
            {
                return;
            }
            EventSink.OnCompleted(ref details, sub);

            if (TraceEvents)
            {
                Trace(subscriptionId, "OnCompleted");
            }
        }

        public void AddOnError(long subscriptionId, Exception e)
        {
            var details = CommonEventDetails.Capture();
            var sub = mSubStore.GetSub(subscriptionId);
            if (sub == null)
            {
                return;
            }
            EventSink.OnError(ref details, sub, e);

            if (TraceEvents)
            {
                Trace(subscriptionId, $"OnError({e.Message})");
            }
        }

        private void Trace(long subId, string message)
        {
            var sub = mSubStore.GetSub(subId);
            System.Diagnostics.Trace.WriteLine($"Obs{sub?.Observable.ObservableId}:Sub{subId}:{message}");
        }
    }
}
