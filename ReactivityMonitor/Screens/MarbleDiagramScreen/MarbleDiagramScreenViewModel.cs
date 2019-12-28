using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.Common;
using ReactivityMonitor.Services;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class MarbleDiagramScreenViewModel : ReactiveViewModel, IMarbleDiagramScreen
    {
        public MarbleDiagramScreenViewModel(IConcurrencyService concurrencyService)
        {
            var items = new ObservableCollectionExtended<ObservableItem>();
            Items = new ReadOnlyObservableCollection<ObservableItem>(items);

            this.WhenActivated(disposables =>
            {
                items.Clear();

                var instances = ObservableInstances
                    .TakeUntilDisposed(disposables)
                    .Publish();

                instances
                    .Transform(obs => Observable.Return(obs).Expand(obs => obs.Inputs).ToObservableChangeSet(obs => obs.ObservableId))
                    .RemoveKey()
                    .AsObservableList()
                    .Or()
                    .Transform(obs => new ObservableItem(concurrencyService) { ObservableInstance = obs })
                    .Sort(SortExpressionComparer<ObservableItem>.Ascending(obs => obs.SequenceId))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(items)
                    .Subscribe()
                    .DisposeWith(disposables);

                instances
                    .Minimum(obs => obs.Created.Timestamp.Ticks)
                    .DistinctUntilChanged()
                    .Select(ticks => new DateTime(ticks, DateTimeKind.Utc))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .ToProperty(this, x => x.StartTime, out mStartTime)
                    .DisposeWith(disposables);

                instances.Connect();
            });
        }

        public IObservable<IChangeSet<IObservableInstance, long>> ObservableInstances { get; set; }

        public ReadOnlyObservableCollection<ObservableItem> Items { get; }

        private ObservableAsPropertyHelper<DateTime> mStartTime;
        public DateTime StartTime => mStartTime.Value;
    }
}
