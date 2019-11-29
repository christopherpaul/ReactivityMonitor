using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface ISubscription
    {
        long SubscriptionId { get; }
        IObservableInstance Observable { get; }

        /// <summary>
        /// All events that (have) happened during the subscription. This is a
        /// cold observable.
        /// </summary>
        IObservable<StreamEvent> Events { get; }
    }
}
