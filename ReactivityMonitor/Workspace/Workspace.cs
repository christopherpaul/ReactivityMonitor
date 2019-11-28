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

        public Workspace()
        {
            mMonitoredCalls = new SourceCache<IMonitoredCall, int>(c => c.Call.InstrumentedCallId);
        }

        public IObservableCache<IMonitoredCall, int> MonitoredCalls => mMonitoredCalls;

        public void StartMonitoringCall(IInstrumentedCall call)
        {
            if (!mMonitoredCalls.Lookup(call.InstrumentedCallId).HasValue)
            {
                mMonitoredCalls.AddOrUpdate(new MonitoredCall(call));
            }
        }

        public void StopMonitoringCall(IInstrumentedCall call)
        {
            mMonitoredCalls.RemoveKey(call.InstrumentedCallId);
        }
    }
}
