using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    internal struct CommonEventDetails
    {
        private static long sEventSequenceIdSource;

        public CommonEventDetails(long eventSequenceId, DateTime timestamp, int threadId)
        {
            EventSequenceId = eventSequenceId;
            Timestamp = timestamp;
            ThreadId = threadId;
        }

        public static CommonEventDetails Capture()
        {
            return new CommonEventDetails(
                Interlocked.Increment(ref sEventSequenceIdSource),
                DateTime.UtcNow, 
                Thread.CurrentThread.ManagedThreadId);
        }

        public long EventSequenceId { get; }
        public DateTime Timestamp { get; }
        public int ThreadId { get; }
    }
}
