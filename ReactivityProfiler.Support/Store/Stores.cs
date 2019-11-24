using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal static class Stores
    {
        static Stores()
        {
            Instrumentation = new InstrumentationStore();
            Subscriptions = new SubscriptionStore();
            RxEvents = new RxEventStore(Subscriptions);
        }

        public static InstrumentationStore Instrumentation { get; }
        public static SubscriptionStore Subscriptions { get; }
        public static RxEventStore RxEvents { get; }
    }
}
