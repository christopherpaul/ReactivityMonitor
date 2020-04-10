using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.EventListScreen;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Dialogs.QuickEventList
{
    public sealed class QuickEventListDialogViewModel : ReactiveViewModel, IQuickEventListDialog, Caliburn.Micro.IHaveDisplayName
    {
        // Inputs
        public string Title { get; set; }
        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }
        public IObservable<ClientEvent> ClientEvents { get; set; }
        public Action<Unit> Proceed { get; set; }
        public Action Cancel { get; set; }

        public QuickEventListDialogViewModel(IEventList eventList)
        {
            eventList.IncludeInputObservables = this.WhenAnyValue(x => x.IncludeInputObservables);
            EventList = eventList;

            var whenUserCancels = CommandHelper.CreateTriggerCommand(out var cancelCommand);
            CancelCommand = cancelCommand;

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                DisplayName = $"{Title} - events";
                eventList.Observables = Observables;
                eventList.ClientEvents = ClientEvents;

                eventList.Activator.Activate().DisposeWith(disposables);

                whenUserCancels.Subscribe(_ => Cancel()).DisposeWith(disposables);
            });
        }

        public string DisplayName { get; set; }
        public IEventList EventList { get; }

        public ICommand CancelCommand { get; }

        private bool mIncludeInputObservables;
        public bool IncludeInputObservables
        {
            get => mIncludeInputObservables;
            set => this.RaiseAndSetIfChanged(ref mIncludeInputObservables, value);
        }
    }
}
