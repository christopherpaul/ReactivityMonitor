using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Screens.MarbleDiagramScreen;
using ReactivityMonitor.Screens.ObservablesScreen;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class MonitoringScreenViewModel : ReactiveViewModel, IMonitoringScreen
    {
        public MonitoringScreenViewModel(IConcurrencyService concurrencyService, IObservablesScreen observablesScreen)
        {
            MainContent = observablesScreen;

            this.WhenActivated(disposables =>
            {
                observablesScreen.WhenIsUpdatingChanges = WhenIsUpdatingChanges;

                observablesScreen.Observables = MonitoringGroup.Calls
                    .MergeMany(call => call.Call.ObservableInstances)
                    .ToObservableChangeSet(obs => obs.ObservableId);

                observablesScreen.Activator.Activate().DisposeWith(disposables);

                MonitoringGroup.WhenNameChanges
                    .ToProperty(this, x => x.Name, out mName)
                    .DisposeWith(disposables);
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }
        public IMonitoringGroup MonitoringGroup { get; set; }

        private ObservableAsPropertyHelper<string> mName;
        public string Name => mName?.Value;

        public object MainContent { get; }
        public IObservable<bool> WhenIsUpdatingChanges { get; set; }
    }
}
