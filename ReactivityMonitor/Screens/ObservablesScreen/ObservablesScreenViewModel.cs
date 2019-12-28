using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public sealed class ObservablesScreenViewModel : ReactiveViewModel, IObservablesScreen
    {
        public ObservablesScreenViewModel(IConcurrencyService concurrencyService, IObservablesScreenItemFactory factory)
        {
            var items = new ObservableCollectionExtended<ObservablesListItem>();
            Items = new ReadOnlyObservableCollection<ObservablesListItem>(items);

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                Observables
                    .Transform(obs => factory.CreateItem(obs))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(items)
                    .Transform(item => item.Activator.Activate())
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }

        public ReadOnlyObservableCollection<ObservablesListItem> Items { get; }
    }
}
