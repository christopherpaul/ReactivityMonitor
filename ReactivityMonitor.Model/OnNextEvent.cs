using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class OnNextEvent : StreamEvent
    {
        public OnNextEvent(EventInfo info, object payload) : base(info)
        {
            Payload = payload;
        }

        public override EventKind Kind => EventKind.OnNext;

        public object Payload { get; }
    }
}
