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
using ReactivityMonitor.Services;
using ReactivityMonitor.Utility;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class MarbleDiagramScreenViewModel : ReactiveViewModel, IMarbleDiagramScreen
    {
        public MarbleDiagramScreenViewModel(IConcurrencyService concurrencyService)
        {
            var itemGroups = new ObservableCollectionExtended<MarbleObservableItemGroup>();
            ObservableItemGroups = new ReadOnlyObservableCollection<MarbleObservableItemGroup>(itemGroups);

            this.WhenActivated(disposables =>
            {
                itemGroups.Clear();

                var instances = ObservableInstances
                    .TakeUntilDisposed(disposables)
                    .Publish();

                var observableItems = instances
                    .Transform(obs => Observable.Return(new MarbleObservableItem(concurrencyService) { ObservableInstance = obs })
                        .Expand(item => item.ObservableInstance.Inputs.Select(input => new MarbleObservableItem(concurrencyService) { ObservableInstance = input, PrimarySink = item }))
                        .ToObservableChangeSet(obs => obs.ObservableInstance.ObservableId))
                    .RemoveKey()
                    .AsObservableList()
                    .Or();

                var observableItemsGroupedByCall = observableItems
                    .Group(item => item.ObservableInstance.Call.InstrumentedCallId)
                    .Transform(group =>
                    {
                        var firstItem = group.Cache.Items.First();
                        var observableItemsInGroup = new ObservableCollectionExtended<MarbleObservableItem>();
                        group.Cache.Connect()
                            .OnItemAdded(obsItem => obsItem.Activator.Activate().DisposeWith(disposables))
                            .Sort(Utility.Comparer<MarbleObservableItem>.ByKey(x => x.GetOrdering(), EnumerableComparer<long>.LongerBeforeShorter))
                            .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                            .ObserveOn(concurrencyService.DispatcherRxScheduler)
                            .Bind(observableItemsInGroup)
                            .Subscribe()
                            .DisposeWith(disposables);

                        return new MarbleObservableItemGroup(firstItem.GetOrdering(), firstItem.ObservableInstance.Call, new ReadOnlyObservableCollection<MarbleObservableItem>(observableItemsInGroup), concurrencyService);
                    });

                observableItemsGroupedByCall
                    .Sort(Utility.Comparer<MarbleObservableItemGroup>.ByKey(x => x.Ordering, EnumerableComparer<long>.LongerBeforeShorter))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(itemGroups)
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

        public ReadOnlyObservableCollection<MarbleObservableItemGroup> ObservableItemGroups { get; }

        private ObservableAsPropertyHelper<DateTime> mStartTime;
        public DateTime StartTime => mStartTime.Value;
    }
}
