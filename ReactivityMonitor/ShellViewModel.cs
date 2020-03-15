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
            IDialogService dialogService, IConcurrencyService concurrencyService,
            IWorkspaceFactory workspaceFactory, ISelectionService selectionService)
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

                this.WhenAnyValue(x => x.ActiveItem)
                    .Subscribe(item => selectionService.ChangeSelection(s => s.SetWorkspace((item as IHomeScreen)?.Workspace)))
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
                    var workspace = workspaceFactory.CreateWorkspace(conn);
                    var homeScreen = screenFactory.CreateHomeScreen(workspace);
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