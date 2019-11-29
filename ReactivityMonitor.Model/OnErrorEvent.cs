using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class OnErrorEvent : StreamEvent
    {
        public OnErrorEvent(EventInfo info, string message) : base(info)
        {
            Message = message;
        }

        public override EventKind Kind => EventKind.OnError;

        public string Message { get; }
    }
}
