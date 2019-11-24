﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class RxEventStore
    {
        private readonly SubscriptionStore mSubStore;

        public RxEventStore(SubscriptionStore subStore)
        {
            mSubStore = subStore;
        }

        public bool TraceEvents { get; } = true;

        public void AddOnNext<T>(long subscriptionId, T value)
        {
            if (TraceEvents)
            {
                Trace(subscriptionId, $"OnNext({value})");
            }
        }

        public void AddOnCompleted(long subscriptionId)
        {
            if (TraceEvents)
            {
                Trace(subscriptionId, "OnCompleted");
            }
        }

        public void AddOnError(long subscriptionId, Exception e)
        {
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
