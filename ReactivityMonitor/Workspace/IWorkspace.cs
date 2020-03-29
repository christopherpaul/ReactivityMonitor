using DynamicData;
using ReactivityMonitor.Connection;
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
        string Name { get; }
        IReactivityModel Model { get; }

        IMonitoringConfiguration MonitoringConfiguration { get; }

        void PauseUpdates();
        void ResumeUpdates();

        void RequestObjectProperties(PayloadObject obj);

        IObservable<IChangeSet<IInstrumentedMethod>> Methods { get; }

        void AddMethod(IInstrumentedMethod method);
        void RemoveMethod(IInstrumentedMethod method);

        IObservable<IChangeSet<IMonitoredCall>> MonitoredCalls { get; }

        IMonitoredCall StartMonitoringCall(IInstrumentedCall call);
        void StopMonitoringCall(IInstrumentedCall call);

        IObservable<IChangeSet<IMonitoringGroup>> MonitoringGroups { get; }

        IMonitoringGroup CreateMonitoringGroup(string name);
        void DeleteMonitoringGroup(IMonitoringGroup group);
    }

    public interface IWorkspaceBuilder : IWorkspace
    {
        void Initialise(IConnectionModel connectionModel);
    }
}
