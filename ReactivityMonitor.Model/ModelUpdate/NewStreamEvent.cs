using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewStreamEvent
    {
        public NewStreamEvent(long subscriptionId, StreamEvent.EventKind kind, EventInfo info, PayloadInfo payload)
        {
            SubscriptionId = subscriptionId;
            Kind = kind;
            Info = info;
            Payload = payload;
        }

        public long SubscriptionId { get; }
        public StreamEvent.EventKind Kind { get; }
        public EventInfo Info { get; }
        public PayloadInfo Payload { get; }
    }
}
