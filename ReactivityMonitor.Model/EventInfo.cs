using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class EventInfo
    {
        public EventInfo(long sequenceId, DateTime timestamp, int threadId)
        {
            SequenceId = sequenceId;
            Timestamp = timestamp;
            ThreadId = threadId;
        }

        public long SequenceId { get; }
        public DateTime Timestamp { get; }
        public int ThreadId { get; }
    }
}
