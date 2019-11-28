using Caliburn.Micro;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.CallsScreen;
using ReactivityMonitor.Screens.MonitoringScreen;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.HomeScreen
{
    public sealed class HomeScreenViewModel : ReactiveScreen, IHomeScreen
    {
        public HomeScreenViewModel(IWorkspace workspace, ICallsScreen callsScreen, IMonitoringScreen monitoringScreen)
        {
            Calls = callsScreen;
            callsScreen.ConductWith(this);

            Monitoring = monitoringScreen;
            monitoringScreen.ConductWith(this);

            WhenActivated(disposables =>
            {
                DisplayName = ConnectionModel.Server.ProcessName;

                callsScreen.Model = ConnectionModel.Model;
                callsScreen.Workspace = workspace;

                monitoringScreen.Model = ConnectionModel.Model;
                monitoringScreen.Workspace = workspace;

                ConnectionModel.Connect().DisposeWith(disposables);
            });
        }

        public IConnectionModel ConnectionModel { get; set; }

        public IMonitoringScreen Monitoring { get; }
        public ICallsScreen Calls { get; }
    }
}
