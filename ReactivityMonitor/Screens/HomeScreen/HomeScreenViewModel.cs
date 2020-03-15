using Caliburn.Micro;
using DynamicData;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Definitions;
using ReactivityMonitor.Dialogs.AddMethod;
using ReactivityMonitor.Dialogs.QuickEventList;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.CallsScreen;
using ReactivityMonitor.Screens.EventListScreen;
using ReactivityMonitor.Screens.MonitoringConfigurationScreen;
using ReactivityMonitor.Screens.MonitoringScreen;
using ReactivityMonitor.Screens.PayloadScreen;
using ReactivityMonitor.Screens.SelectedCallsScreen;
using ReactivityMonitor.Services;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Screens.HomeScreen
{
    public sealed class HomeScreenViewModel : ReactiveScreen, IHomeScreen
    {
        public HomeScreenViewModel(
            IScreenFactory screenFactory,
            IConcurrencyService concurrencyService,
            ICommandHandlerService commandHandlerService,
            IConnectionService connectionService,
            IMonitoringConfigurationScreen configScreen,
            IDialogService dialogService,
            IAddMethodDialog addMethodDialog,
            ISelectionService selectionService,
            IQuickEventListDialog quickEventListDialog,
            ISelectedCallsScreen selectedCallsScreen)
        {
            SelectedCallsScreen = selectedCallsScreen;

            var isUpdating = GoPauseControl.SetupGoPause(out var attachGoPauseHandlers)
                .ObserveOn(concurrencyService.TaskPoolRxScheduler);

            WhenActivated(disposables =>
            {
                DisplayName = Workspace.Name;

                isUpdating.Subscribe(x =>
                {
                    if (x)
                    {
                        Workspace.ResumeUpdates();
                    }
                    else
                    {
                        Workspace.PauseUpdates();
                    }
                }).DisposeWith(disposables);

                attachGoPauseHandlers(commandHandlerService).DisposeWith(disposables);

                var closeCommand = ReactiveUI.ReactiveCommand.Create(() => connectionService.Close());
                commandHandlerService.RegisterHandler(Commands.CloseWorkspace, closeCommand)
                    .DisposeWith(disposables);

                addMethodDialog.Model = Workspace.Model;
                var addMethodToConfigCommand = ReactiveUI.ReactiveCommand.Create(async () =>
                {
                    var methodToAdd = await dialogService.ShowDialogContent(addMethodDialog);
                    Workspace.AddMethod(methodToAdd);
                });
                commandHandlerService.RegisterHandler(Commands.ShowAddToConfiguration, addMethodToConfigCommand)
                    .DisposeWith(disposables);

                var quickEventListCommand = ReactiveUI.ReactiveCommand.Create(async () =>
                {
                    var call = selectionService.CurrentSelection.PrimaryInstrumentedCall;
                    if (call != null)
                    {
                        quickEventListDialog.Observables = call.ObservableInstances.ToObservableChangeSet(o => o.ObservableId);
                        quickEventListDialog.Title = $"{call.Method.Name}: {call.CalledMethod}";
                        await dialogService.ShowDialogContent(quickEventListDialog);
                    }
                }, selectionService.WhenSelectionChanges.Select(s => s.PrimaryInstrumentedCall != null).ObserveOn(concurrencyService.DispatcherRxScheduler));
                commandHandlerService.RegisterHandler(Commands.QuickEventList, quickEventListCommand)
                    .DisposeWith(disposables);

                // Document screens
                Observable.Empty<IWorkspaceDocumentScreen>()
                    .StartWith(configScreen)
                    .ToObservableChangeSet()
                    .OnItemAdded(s =>
                    {
                        s.Workspace = Workspace;
                    })
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var documentScreens)
                    .Subscribe()
                    .DisposeWith(disposables);

                DocumentScreens = documentScreens;
            });
        }

        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<IWorkspaceDocumentScreen> DocumentScreens { get; private set; }

        private IWorkspaceDocumentScreen mActiveDocumentScreen;
        public IWorkspaceDocumentScreen ActiveDocumentScreen
        {
            get => mActiveDocumentScreen;
            set => Set(ref mActiveDocumentScreen, value);
        }

        public ISelectedCallsScreen SelectedCallsScreen { get; }
    }
}
