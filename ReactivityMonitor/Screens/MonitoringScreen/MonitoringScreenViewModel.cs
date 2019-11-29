using DynamicData;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class MonitoringScreenViewModel : ReactiveScreen, IMonitoringScreen
    {
        public MonitoringScreenViewModel(IConcurrencyService concurrencyService)
        {
            WhenActivated(disposables =>
            {
                Model.ObservableInstances.Connect()
                    .Transform(obs => new Item(obs))
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var observableInstances)
                    .Subscribe()
                    .DisposeWith(disposables);

                Items = observableInstances;
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<Item> Items { get; private set; }

        public sealed class Item
        {
            private readonly IObservableInstance mObs;

            public Item(IObservableInstance obs)
            {
                mObs = obs;
            }

            public long SequenceId => mObs.Created.SequenceId;
            public DateTime Timestamp => mObs.Created.Timestamp;
            public long ThreadId => mObs.Created.ThreadId;

            public string MethodName => mObs.Call.CalledMethod;
        }
    }
}
