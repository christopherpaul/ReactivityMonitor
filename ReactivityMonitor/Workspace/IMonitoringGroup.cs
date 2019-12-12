using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Workspace
{
    public interface IMonitoringGroup
    {
        IObservable<string> WhenNameChanges { get; }
        IObservable<IChangeSet<IMonitoredCall>> Calls { get; }

        void SetName(string name);

        void AddCall(IMonitoredCall call);
        void RemoveCall(IMonitoredCall call);
    }
}
