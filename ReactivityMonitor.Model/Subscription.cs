using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class Subscription : ISubscription
    {
        public Subscription(long subId, IObservableInstance observableInstance, IObservable<StreamEvent> events)
        {
            SubscriptionId = subId;
            Observable = observableInstance;
            Events = events;
        }

        public long SubscriptionId { get; }

        public IObservableInstance Observable { get; }

        public IObservable<StreamEvent> Events { get; }
    }
}
