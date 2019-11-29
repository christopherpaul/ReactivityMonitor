using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class SubscribeEvent : StreamEvent
    {
        public SubscribeEvent(EventInfo info) : base(info)
        {
        }

        public override EventKind Kind => EventKind.Subscribe;
    }
}
