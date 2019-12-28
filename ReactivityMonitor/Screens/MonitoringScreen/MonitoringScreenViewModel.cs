using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Utility.Extensions;
using ReactivityMonitor.Services;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using ReactivityMonitor.Screens.Common;
using ReactivityMonitor.Screens.MarbleDiagramScreen;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class MonitoringScreenViewModel : ReactiveViewModel, IMonitoringScreen
    {
        public MonitoringScreenViewModel(IConcurrencyService concurrencyService, IMarbleDiagramScreen marbleDiagram)
        {
            MarbleDiagram = marbleDiagram;

            this.WhenActivated(disposables =>
            {
                marbleDiagram.ObservableInstances = MonitoringGroup.Calls
                    .MergeMany(call => call.Call.ObservableInstances)
                    .ToObservableChangeSet(obs => obs.ObservableId);

                marbleDiagram.Activator.Activate().DisposeWith(disposables);

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

        public IMarbleDiagramScreen MarbleDiagram { get; }
    }
}
