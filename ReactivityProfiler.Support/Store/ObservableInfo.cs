using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    /// <summary>
    /// Holds information about an IObservable instance.
    /// </summary>
    internal sealed class ObservableInfo
    {
        private static int sCurrentMonitoringRevision;
        private int mMonitoringRevision = -1;

        public ObservableInfo(int instrumentationPoint, IReadOnlyList<ObservableInfo> inputs)
        {
            Details = CommonEventDetails.Capture();
            InstrumentationPoint = instrumentationPoint;
            Inputs = inputs;
        }

        public long ObservableId => Details.EventSequenceId;
        public int InstrumentationPoint { get; }
        public IReadOnlyList<ObservableInfo> Inputs { get; }
        public CommonEventDetails Details { get; }

        public bool Monitoring
        {
            get => mMonitoringRevision == sCurrentMonitoringRevision;
            set
            {
                if (value)
                {
                    mMonitoringRevision = sCurrentMonitoringRevision;
                }
                else
                {
                    mMonitoringRevision = -1;
                }
            }
        }

        public static void StopMonitoringAll()
        {
            sCurrentMonitoringRevision++;
        }
    }
}
