using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class DisposedSubscription
    {
        public DisposedSubscription(EventInfo disposed, long subscriptionId)
        {
            Disposed = disposed;
            SubscriptionId = subscriptionId;
        }

        public EventInfo Disposed { get; }
        public long SubscriptionId { get; }
    }
}
