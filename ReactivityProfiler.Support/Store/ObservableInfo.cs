using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    /// <summary>
    /// Holds information about an IObservable instance.
    /// </summary>
    internal sealed class ObservableInfo : IObservableInput
    {
        private static int sCurrentMonitoringRevision;
        private int mMonitoringRevision = -1;
        private readonly ConcurrentDictionary<ObservableInfo, bool> mInputs = new ConcurrentDictionary<ObservableInfo, bool>();

        public ObservableInfo(int instrumentationPoint)
        {
            Details = CommonEventDetails.Capture();
            InstrumentationPoint = instrumentationPoint;
        }

        public long ObservableId => Details.EventSequenceId;
        public int InstrumentationPoint { get; }
        public IEnumerable<ObservableInfo> Inputs => mInputs.Keys;
        public CommonEventDetails Details { get; }

        public void AddInput(ObservableInfo info)
        {
            if (mInputs.TryAdd(info, true))
            {
                Services.Store.NotifyObservablesLinked(this, info);
            }
        }

        public void RemoveInput(ObservableInfo info)
        {
            if (mInputs.TryRemove(info, out _))
            {
                Services.Store.NotifyObservablesUnlinked(this, info);
            }
        }

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

        internal static int CurrentMonitoringRevision => sCurrentMonitoringRevision;

        public static void StopMonitoringAll()
        {
            sCurrentMonitoringRevision++;
        }

        public void AssociateWith(ObservableInfo info)
        {
            info.AddInput(this);
        }
    }
}
