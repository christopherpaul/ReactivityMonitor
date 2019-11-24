using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    internal struct CommonEventDetails
    {
        public CommonEventDetails(DateTime timestamp, int threadId)
        {
            Timestamp = timestamp;
            ThreadId = threadId;
        }

        public static CommonEventDetails Capture()
        {
            return new CommonEventDetails(DateTime.UtcNow, Thread.CurrentThread.ManagedThreadId);
        }

        public DateTime Timestamp { get; }
        public int ThreadId { get; }
    }
}
