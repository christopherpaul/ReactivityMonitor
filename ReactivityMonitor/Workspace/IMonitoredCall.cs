using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Workspace
{
    public interface IMonitoredCall
    {
        IInstrumentedCall Call { get; }
    }
}
