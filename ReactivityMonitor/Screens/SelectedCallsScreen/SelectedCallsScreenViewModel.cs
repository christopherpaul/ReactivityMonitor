using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.ObservablesScreen;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using DynamicData;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.SelectedCallsScreen
{
    public sealed class SelectedCallsScreenViewModel : ReactiveViewModel, ISelectedCallsScreen
    {
        public SelectedCallsScreenViewModel(ISelectionService selectionService, IConcurrencyService concurrencyService,
            IObservablesList observablesListViewModel)
        {
            ObservablesList = observablesListViewModel;
            DisplayName = "IObservable instances";

            observablesListViewModel.Observables = selectionService.WhenSelectionChanges
                .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                .Select(selection => selection.SelectedInstrumentedCalls)
                .DistinctUntilChanged()
                .Select(calls => calls
                    .Select(c => c.ObservableInstances)
                    .Merge()
                    .ToObservableChangeSet(obs => obs.ObservableId))
                .SwitchFixed();

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                observablesListViewModel.WhenSelectionChanges
                    .OnItemAdded(obs => selectionService.ChangeSelection(s => s.AddObservableInstance(obs)))
                    .OnItemRemoved(obs => selectionService.ChangeSelection(s => s.RemoveObservableInstance(obs)))
                    .Subscribe()
                    .DisposeWith(disposables);

                observablesListViewModel.Activator.Activate()
                    .DisposeWith(disposables);
            });
        }

        public IObservablesList ObservablesList { get; }

        public string DisplayName { get; }
    }
}
