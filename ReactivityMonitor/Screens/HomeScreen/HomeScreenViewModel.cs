using DynamicData;
using ReactivityMonitor.Definitions;
using ReactivityMonitor.Dialogs.AddMethod;
using ReactivityMonitor.Dialogs.QuickEventList;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.MonitoringConfigurationScreen;
using ReactivityMonitor.Screens.PayloadScreen;
using ReactivityMonitor.Screens.SelectedCallsScreen;
using ReactivityMonitor.Services;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace ReactivityMonitor.Screens.HomeScreen
{
    public sealed class HomeScreenViewModel : ReactiveScreen, IHomeScreen
    {
        public HomeScreenViewModel(
            IScreenFactory screenFactory,
            IConcurrencyService concurrencyService,
            ICommandHandlerService commandHandlerService,
            IConnectionService connectionService,
            IDialogService dialogService,
            IAddMethodDialog addMethodDialog,
            ISelectionService selectionService,
            IQuickEventListDialog quickEventListDialog,
            ISelectedCallsScreen selectedCallsScreen,
            IPayloadScreen payloadScreen)
        {
            SelectedCallsScreen = selectedCallsScreen;
            PayloadScreen = payloadScreen;

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
                    Workspace.AddSourceMethod(methodToAdd);
                });
                commandHandlerService.RegisterHandler(Commands.ShowAddToConfiguration, addMethodToConfigCommand)
                    .DisposeWith(disposables);

                var selectionHasItemsWithEvents = selectionService.WhenSelectionChanges.Select(s => s.PrimaryInstrumentedCall != null || s.SelectedObservableInstances.Count > 0).ObserveOn(concurrencyService.DispatcherRxScheduler);

                var quickEventListCommand = ReactiveUI.ReactiveCommand.Create(async () =>
                {
                    Selection sel = selectionService.CurrentSelection;
                    bool haveObservables = false;

                    if (sel.SelectedObservableInstances.Count > 0)
                    {
                        haveObservables = true;
                        quickEventListDialog.Observables = sel.SelectedObservableInstances
                            .ToObservable()
                            .ToObservableChangeSet(o => o.ObservableId);

                        if (sel.SelectedObservableInstances.Count == 1)
                        {
                            var obs = sel.SelectedObservableInstances[0];
                            var call = obs.Call;
                            quickEventListDialog.Title = $"{call.Method.DetailName}: {call.CalledMethod} @{obs.Created.Timestamp}";
                        }
                        else
                        {
                            quickEventListDialog.Title = $"{sel.SelectedObservableInstances.Count} IObservable instances";
                        }
                    }
                    else
                    {
                        var call = sel.PrimaryInstrumentedCall;
                        if (call != null)
                        {
                            haveObservables = true;
                            quickEventListDialog.Observables = call.ObservableInstances.ToObservableChangeSet(o => o.ObservableId);
                            quickEventListDialog.Title = $"{call.Method.DetailName}: {call.CalledMethod}";
                        }
                    }

                    if (haveObservables)
                    {
                        await dialogService.ShowDialogContent(quickEventListDialog);
                    }

                }, selectionHasItemsWithEvents);
                commandHandlerService.RegisterHandler(Commands.QuickEventList, quickEventListCommand)
                    .DisposeWith(disposables);

                var openEventListCommand = ReactiveUI.ReactiveCommand.Create(() =>
                {
                    Selection sel = selectionService.CurrentSelection;

                    if (sel.SelectedObservableInstances.Count > 0)
                    {
                        Workspace.CreateEventsDocument(sel.SelectedObservableInstances);
                    }
                    else if (sel.PrimaryInstrumentedCall != null)
                    {
                        Workspace.CreateEventsDocument(new[] { sel.PrimaryInstrumentedCall });
                    }

                }, selectionHasItemsWithEvents);
                commandHandlerService.RegisterHandler(Commands.OpenEventList, openEventListCommand)
                    .DisposeWith(disposables);

                // Document screens
                Workspace.Documents
                    .Transform(screenFactory.CreateDocumentScreen)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var documentScreens)
                    .OnItemAdded(screen => ActiveDocumentScreen = screen)
                    .Transform(screen => screen.Activator.Activate())
                    .DisposeMany()
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
        public IPayloadScreen PayloadScreen { get; }
    }
}
