using DynamicData;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class MonitoringScreenViewModel : ReactiveScreen, IMonitoringScreen
    {
        public MonitoringScreenViewModel()
        {
            WhenActivated(disposables =>
            {
                Workspace.MonitoredCalls.Connect()
                    .Transform(call => (object)new MonitoredCallViewModel(call))
                    .Bind(out var monitoredCalls)
                    .Subscribe()
                    .DisposeWith(disposables);

                MonitoredCalls = monitoredCalls;
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<object> MonitoredCalls { get; private set; }

        private sealed class MonitoredCallViewModel
        {
            public MonitoredCallViewModel(IMonitoredCall call)
            {
                Call = call;
            }

            public string Title => Call.Call.CalledMethod;
            public IMonitoredCall Call { get; }
        }
    }
}
