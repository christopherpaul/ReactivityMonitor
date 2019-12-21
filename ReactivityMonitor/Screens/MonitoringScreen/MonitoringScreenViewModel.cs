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
    public sealed class MonitoringScreenViewModel : ReactiveViewModel, IMonitoringScreen
    {
        public MonitoringScreenViewModel(IConcurrencyService concurrencyService)
        {
            this.WhenActivated(disposables =>
            {
                MonitoringGroup.WhenNameChanges
                    .Subscribe(n => Name = n)
                    .DisposeWith(disposables);

                // TODO: this doesn't deal properly with a call being removed from the group
                MonitoringGroup.Calls
                    .MergeMany(call => call.Call.ObservableInstances)
                    .Expand(obs => obs.Inputs)
                    .ToObservableChangeSet(obs => obs.ObservableId)
                    .Transform(obs => new ObservableItem(concurrencyService) { ObservableInstance = obs })
                    .Sort(SortExpressionComparer<ObservableItem>.Ascending(obs => obs.SequenceId))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
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
        public IMonitoringGroup MonitoringGroup { get; set; }

        private string mName;
        public string Name
        {
            get => mName;
            private set => this.RaiseAndSetIfChanged(ref mName, value);
        }

        private ReadOnlyObservableCollection<ObservableItem> mItems;
        public ReadOnlyObservableCollection<ObservableItem> Items
        {
            get => mItems;
            private set => this.RaiseAndSetIfChanged(ref mItems, value);
        }

        private DateTime mStartTime;
        public DateTime StartTime
        {
            get => mStartTime;
            private set => this.RaiseAndSetIfChanged(ref mStartTime, value);
        }
    }
}
