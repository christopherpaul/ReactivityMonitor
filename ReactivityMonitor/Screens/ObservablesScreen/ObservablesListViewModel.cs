using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public sealed class ObservablesListViewModel : ReactiveViewModel, IObservablesList
    {
        private readonly ISubject<IChangeSet<ObservablesListItem>> mWhenSelectionChanges = new Subject<IChangeSet<ObservablesListItem>>();

        public ObservablesListViewModel(IConcurrencyService concurrencyService, IObservablesScreenItemFactory factory)
        {
            var items = new ObservableCollectionExtended<ObservablesListItem>();
            Items = new ReadOnlyObservableCollection<ObservablesListItem>(items);

            WhenSelectionChanges = mWhenSelectionChanges
                .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                .AddKey(item => item.ObservableInstance.ObservableId)
                .Transform(item => item.ObservableInstance)
                .AsObservableCache()
                .Connect();

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                Observables
                    .Transform(obs => factory.CreateItem(obs))
                    .Sort(Utility.Comparer<ObservablesListItem>.ByKey(x => -x.ObservableInstance.ObservableId))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(items)
                    .Transform(item => item.Activator.Activate())
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        internal void OnSelectedItemsChanged(IChangeSet<ObservablesListItem> changeSet)
        {
            mWhenSelectionChanges.OnNext(changeSet);
        }

        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }

        public ReadOnlyObservableCollection<ObservablesListItem> Items { get; }

        public IObservable<IChangeSet<IObservableInstance, long>> WhenSelectionChanges { get; }
    }
}
