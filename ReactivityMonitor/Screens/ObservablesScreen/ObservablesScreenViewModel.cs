using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.MarbleDiagramScreen;
using ReactivityMonitor.Services;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public sealed class ObservablesScreenViewModel : ReactiveViewModel, IObservablesScreen
    {
        private readonly ISubject<IChangeSet<ObservablesListItem>> mWhenSelectionChanges = new Subject<IChangeSet<ObservablesListItem>>();

        public ObservablesScreenViewModel(IConcurrencyService concurrencyService, IObservablesScreenItemFactory factory, IMarbleDiagramScreen marbleDiagram)
        {
            var items = new ObservableCollectionExtended<ObservablesListItem>();
            Items = new ReadOnlyObservableCollection<ObservablesListItem>(items);

            DetailContent = marbleDiagram;

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

                marbleDiagram.ObservableInstances = mWhenSelectionChanges
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .AddKey(item => item.ObservableInstance.ObservableId)
                    .Transform(item => item.ObservableInstance);

                marbleDiagram.Activator.Activate().DisposeWith(disposables);
            });
        }

        public void OnSelectedItemsChanged(IChangeSet<ObservablesListItem> changes)
        {
            mWhenSelectionChanges.OnNext(changes);
        }

        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }

        public ReadOnlyObservableCollection<ObservablesListItem> Items { get; }

        public object DetailContent { get; private set; }
    }
}
