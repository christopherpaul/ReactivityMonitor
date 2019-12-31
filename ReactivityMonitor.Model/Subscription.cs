using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class Subscription : ISubscription
    {
        public Subscription(long subId, IObservable<IObservableInstance> observableInstance, IObservable<StreamEvent> events)
        {
            SubscriptionId = subId;
            observableInstance.Subscribe(obs => Observable = obs);
            Events = events;
        }

        public long SubscriptionId { get; }

        public IObservableInstance Observable { get; private set; }

        public IObservable<StreamEvent> Events { get; }
    }
}
