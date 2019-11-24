using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class SubscriptionInfo
    {
        private static long sSubscriptionIdSource;

        public SubscriptionInfo(ObservableInfo observable)
        {
            SubscriptionId = Interlocked.Increment(ref sSubscriptionIdSource);
            Details = CommonEventDetails.Capture();
            Observable = observable;
        }

        public long SubscriptionId { get; }
        public ObservableInfo Observable { get; }
        public CommonEventDetails Details { get; }
    }
}
