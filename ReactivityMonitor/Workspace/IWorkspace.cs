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
        IObservable<IChangeSet<IMonitoredCall>> MonitoredCalls { get; }

        IMonitoredCall StartMonitoringCall(IInstrumentedCall call);
        void StopMonitoringCall(IInstrumentedCall call);

        IObservable<IChangeSet<IMonitoringGroup>> MonitoringGroups { get; }

        IMonitoringGroup CreateMonitoringGroup(string name);
        void DeleteMonitoringGroup(IMonitoringGroup group);
    }
}
