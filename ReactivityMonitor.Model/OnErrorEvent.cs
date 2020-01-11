using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class OnErrorEvent : StreamEvent
    {
        public OnErrorEvent(EventInfo info, object exception) : base(info)
        {
            Exception = exception;
        }

        public override EventKind Kind => EventKind.OnError;

        public string Message => Exception?.ToString();

        public object Exception { get; }
    }
}
