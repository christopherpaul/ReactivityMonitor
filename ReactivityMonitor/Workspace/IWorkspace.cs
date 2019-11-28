using DynamicData;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Workspace
{
    public interface IWorkspace
    {
        IObservableCache<IMonitoredCall, int> MonitoredCalls { get; }

        void StartMonitoringCall(IInstrumentedCall call);
        void StopMonitoringCall(IInstrumentedCall call);
    }
}
