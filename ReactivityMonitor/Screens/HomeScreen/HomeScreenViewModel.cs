using Caliburn.Micro;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.CallsScreen;
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
        public HomeScreenViewModel(ICallsScreen callsScreen)
        {
            Calls = callsScreen;
            callsScreen.ConductWith(this);

            WhenActivated(disposables =>
            {
                DisplayName = ConnectionModel.Server.ProcessName;
                callsScreen.Model = ConnectionModel.Model;

                ConnectionModel.Connect().DisposeWith(disposables);
            });
        }

        public IConnectionModel ConnectionModel { get; set; }

        public ICallsScreen Calls { get; }
    }
}
