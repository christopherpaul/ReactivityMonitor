using Caliburn.Micro;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Screens.CallsScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.HomeScreen
{
    public interface IHomeScreen : IScreen
    {
        IConnectionModel ConnectionModel { get; set; }

        ICallsScreen Calls { get; }
    }
}
