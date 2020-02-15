using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using ReactivityMonitor.Model;

namespace ReactivityMonitor.Workspace
{
    public sealed class Workspace : IWorkspace
    {
        private ISourceCache<IMonitoredCall, int> mMonitoredCalls;
        private ISourceList<IMonitoringGroup> mMonitoringGroups;
        private ISourceCache<IInstrumentedMethod, int> mMethods;

        public Workspace()
        {
            mMonitoredCalls = new SourceCache<IMonitoredCall, int>(c => c.Call.InstrumentedCallId);
            mMonitoringGroups = new SourceList<IMonitoringGroup>();
            mMethods = new SourceCache<IInstrumentedMethod, int>(m => m.InstrumentedMethodId);

            MonitoredCalls = mMonitoredCalls.Connect().RemoveKey();
            MonitoringGroups = mMonitoringGroups.Connect();
            Methods = mMethods.Connect().RemoveKey();
        }

        public IObservable<IChangeSet<IInstrumentedMethod>> Methods { get; }

        public IObservable<IChangeSet<IMonitoredCall>> MonitoredCalls { get; }

        public IObservable<IChangeSet<IMonitoringGroup>> MonitoringGroups { get; }

        public IMonitoringGroup CreateMonitoringGroup(string name)
        {
            var mg = new MonitoringGroup(name, mMonitoredCalls);
            mMonitoringGroups.Add(mg);
            return mg;
        }

        public void DeleteMonitoringGroup(IMonitoringGroup group)
        {
            mMonitoringGroups.Remove(group);
        }

        public void AddMethod(IInstrumentedMethod method)
        {
            mMethods.AddOrUpdate(method);
        }

        public void RemoveMethod(IInstrumentedMethod method)
        {
            mMethods.RemoveKey(method.InstrumentedMethodId);
        }

        public IMonitoredCall StartMonitoringCall(IInstrumentedCall call)
        {
            var monitoredCallOpt = mMonitoredCalls.Lookup(call.InstrumentedCallId);
            if (!monitoredCallOpt.HasValue)
            {
                var monitoredCall = new MonitoredCall(call);
                mMonitoredCalls.AddOrUpdate(monitoredCall);
                return monitoredCall;
            }

            return monitoredCallOpt.Value;
        }

        public void StopMonitoringCall(IInstrumentedCall call)
        {
            mMonitoredCalls.RemoveKey(call.InstrumentedCallId);
        }
    }
}
