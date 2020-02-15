using System;
using Caliburn.Micro;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Services;
using ReactivityMonitor.Screens.HomeScreen;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using ReactivityMonitor.Connection;
using ReactiveUI;
using IScreen = Caliburn.Micro.IScreen;
using ReactivityMonitor.Screens;
using System.Windows.Input;

namespace ReactivityMonitor
{
    public class ShellViewModel : ReactiveConductor<IScreen>, IShell
    {
        public ShellViewModel(IConnectionService connectionService, IScreenFactory screenFactory,
            IDialogService dialogService, IConcurrencyService concurrencyService)
        {
            DisplayName = "Reactivity Monitor";

            var concreteDialogService = (DialogService)dialogService;

            WhenActivated(disposables =>
            {
                connectionService.WhenConnectionChanges
                    .ObserveOnDispatcher()
                    .Select(GetViewModelForConnection)
                    .Subscribe(ActivateItem)
                    .DisposeWith(disposables);

                this.ObservableForProperty(x => x.ActiveItem).Value()
                    .Select(item => item.WhenAnyValue(x => x.DisplayName))
                    .Switch()
                    .Select(itemTitle => $"{itemTitle} - Reactivity Monitor")
                    .Subscribe(title => DisplayName = title)
                    .DisposeWith(disposables);

                Disposable.Create(connectionService.Close).DisposeWith(disposables);

                concreteDialogService.WhenDialogViewModelChanges
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Subscribe(vm => DialogViewModel = vm)
                    .DisposeWith(disposables);
            });

            CancelDialogCommand = ReactiveCommand.Create(concreteDialogService.CancelActiveDialog);

            IScreen GetViewModelForConnection(IConnectionModel conn)
            {
                if (conn == null)
                {
                    return screenFactory.CreateConnectionScreen();
                }
                else
                {
                    var homeScreen = screenFactory.CreateHomeScreen();
                    homeScreen.ConnectionModel = conn;
                    return homeScreen;
                }
            }
        }

        private object mDialogViewModel;
        public object DialogViewModel
        {
            get => mDialogViewModel;
            private set 
            { 
                if (Set(ref mDialogViewModel, value))
                {
                    NotifyOfPropertyChange(nameof(DialogTitle));
                }
            }
        }

        public ICommand CancelDialogCommand { get; }

        public string DialogTitle => (DialogViewModel as IHaveDisplayName)?.DisplayName;
    }
}