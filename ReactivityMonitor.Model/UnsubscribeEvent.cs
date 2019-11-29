using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class UnsubscribeEvent : StreamEvent
    {
        public UnsubscribeEvent(EventInfo info) : base(info)
        {
        }

        public override EventKind Kind => EventKind.Unsubscribe;
    }
}
