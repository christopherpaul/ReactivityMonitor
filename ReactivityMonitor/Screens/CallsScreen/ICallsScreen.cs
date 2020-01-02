using Caliburn.Micro;
using ReactivityMonitor.Model;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Screens.CallsScreen
{
    public interface ICallsScreen : IScreen
    {
        IReactivityModel Model { get; set; }
        IWorkspace Workspace { get; set; }
    }
}
