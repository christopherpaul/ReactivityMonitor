using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Model;

namespace ReactivityMonitor.Workspace
{
    public sealed class MonitoredCall : IMonitoredCall
    {
        public MonitoredCall(IInstrumentedCall call)
        {
            Call = call;
        }

        public IInstrumentedCall Call { get; }
    }
}
