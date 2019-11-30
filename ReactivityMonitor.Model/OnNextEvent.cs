using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class OnNextEvent : StreamEvent
    {
        public OnNextEvent(EventInfo info, string valueString) : base(info)
        {
            ValueString = valueString;
        }

        public override EventKind Kind => EventKind.OnNext;

        public string ValueString { get; }
    }
}
