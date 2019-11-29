using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public abstract class StreamEvent
    {
        public enum EventKind
        {
            Subscribe,
            Unsubscribe,
            OnNext,
            OnCompleted,
            OnError
        }

        protected StreamEvent(EventInfo info)
        {
            Info = info;
        }

        public EventInfo Info { get; }
        public abstract EventKind Kind { get; }
    }
}
