using Caliburn.Micro;
using ReactivityMonitor.Model;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public interface IMonitoringScreen : IScreen
    {
        IReactivityModel Model { get; set; }
        IWorkspace Workspace { get; set; }
    }
}
