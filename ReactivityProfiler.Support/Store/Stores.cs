using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal static class Stores
    {
        static Stores()
        {
            Subscriptions = new SubscriptionStore();
            RxEvents = new RxEventStore(Subscriptions);
        }

        public static SubscriptionStore Subscriptions { get; }
        public static RxEventStore RxEvents { get; }
    }
}
