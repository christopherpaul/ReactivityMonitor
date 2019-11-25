using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class Store : IStore
    {
        public Store()
        {
            Instrumentation = new InstrumentationStore();
            Subscriptions = new SubscriptionStore();
            RxEvents = new RxEventStore(Subscriptions);
        }

        public IInstrumentationStore Instrumentation { get; }
        public ISubscriptionStore Subscriptions { get; }
        public IRxEventStore RxEvents { get; }
    }
}
