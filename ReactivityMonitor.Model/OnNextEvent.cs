using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class OnNextEvent : StreamEvent
    {
        public OnNextEvent(EventInfo info) : base(info)
        {
        }

        public override EventKind Kind => EventKind.OnNext;
    }
}
