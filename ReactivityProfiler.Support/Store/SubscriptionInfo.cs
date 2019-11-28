using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class SubscriptionInfo
    {
        public SubscriptionInfo(ObservableInfo observable)
        {
            Details = CommonEventDetails.Capture();
            Observable = observable;
        }

        public long SubscriptionId => Details.EventSequenceId;
        public ObservableInfo Observable { get; }
        public CommonEventDetails Details { get; }
    }
}
