using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class SubscriptionInfo
    {
        private int mMonitoringRevision = -1;

        public SubscriptionInfo(ObservableInfo observable)
        {
            Details = CommonEventDetails.Capture();
            Observable = observable;
        }

        public long SubscriptionId => Details.EventSequenceId;
        public ObservableInfo Observable { get; }
        public CommonEventDetails Details { get; }

        public bool Monitoring
        {
            get => mMonitoringRevision == ObservableInfo.CurrentMonitoringRevision;
            set
            {
                if (value)
                {
                    mMonitoringRevision = ObservableInfo.CurrentMonitoringRevision;
                }
                else
                {
                    mMonitoringRevision = -1;
                }
            }
        }
    }
}
