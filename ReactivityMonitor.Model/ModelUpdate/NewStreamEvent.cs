using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewStreamEvent
    {
        public NewStreamEvent(long subscriptionId, StreamEvent streamEvent)
        {
            SubscriptionId = subscriptionId;
            StreamEvent = streamEvent;
        }

        public long SubscriptionId { get; }
        public StreamEvent StreamEvent { get; }
    }
}
