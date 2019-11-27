using Caliburn.Micro;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.CallsScreen
{
    public interface ICallsScreen : IScreen
    {
        IReactivityModel Model { get; set; }

        ReadOnlyObservableCollection<ICallingMethod> CallingMethods { get; }
    }
}
