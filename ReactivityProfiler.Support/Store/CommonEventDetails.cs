using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    internal struct CommonEventDetails
    {
        public CommonEventDetails(long eventSequenceId, DateTime timestamp, int threadId)
        {
            EventSequenceId = eventSequenceId;
            Timestamp = timestamp;
            ThreadId = threadId;
        }

        public static CommonEventDetails Capture()
        {
            return new CommonEventDetails(
                SequenceIdSource.GetNext(),
                DateTime.UtcNow, 
                Thread.CurrentThread.ManagedThreadId);
        }

        public long EventSequenceId { get; }
        public DateTime Timestamp { get; }
        public int ThreadId { get; }

        private unsafe static class SequenceIdSource
        {
            private static long* sEventSequenceIdSource;

            static SequenceIdSource()
            {
                sEventSequenceIdSource = NativeMethods.GetCommonSequenceIdSource();
            }

            public static long GetNext()
            {
                return Interlocked.Increment(ref *sEventSequenceIdSource);
            }
        }
    }
}
