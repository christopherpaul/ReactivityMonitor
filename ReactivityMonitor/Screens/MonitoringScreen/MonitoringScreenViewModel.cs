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

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class MonitoringScreenViewModel : ReactiveScreen, IMonitoringScreen
    {
        public MonitoringScreenViewModel(IConcurrencyService concurrencyService)
        {
            WhenActivated(disposables =>
            {
                Model.ObservableInstances
                    .Select(obs => new ObservableItem(concurrencyService) { ObservableInstance = obs })
                    .AsChangeSets()
                    .Sort(SortExpressionComparer<ObservableItem>.Ascending(obs => obs.SequenceId))
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var observableInstances)
                    .Subscribe()
                    .DisposeWith(disposables);

                Model.ObservableInstances
                    .Scan(long.MaxValue, (minSoFar, obs) => Math.Min(minSoFar, obs.Created.Timestamp.Ticks))
                    .DistinctUntilChanged()
                    .Subscribe(timestamp => StartTime = new DateTime(timestamp, DateTimeKind.Utc))
                    .DisposeWith(disposables);

                Items = observableInstances;
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<ObservableItem> Items { get; private set; }

        private DateTime mStartTime;
        public DateTime StartTime
        {
            get => mStartTime;
            private set => Set(ref mStartTime, value);
        }
    }
}
