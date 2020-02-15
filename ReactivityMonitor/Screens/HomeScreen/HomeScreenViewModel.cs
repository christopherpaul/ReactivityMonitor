using Caliburn.Micro;
using DynamicData;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Definitions;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.CallsScreen;
using ReactivityMonitor.Screens.EventListScreen;
using ReactivityMonitor.Screens.MonitoringConfigurationScreen;
using ReactivityMonitor.Screens.MonitoringScreen;
using ReactivityMonitor.Screens.PayloadScreen;
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
            IWorkspace workspace, 
            IScreenFactory screenFactory,
            IConcurrencyService concurrencyService,
            ICommandHandlerService commandHandlerService,
            IConnectionService connectionService,
            IMonitoringConfigurationScreen configScreen)
        {
            var isUpdating = GoPauseControl.SetupGoPause(out var attachGoPauseHandlers)
                .ObserveOn(concurrencyService.TaskPoolRxScheduler);

            WhenActivated(disposables =>
            {
                DisplayName = ConnectionModel.Name;

                isUpdating.Subscribe(x =>
                {
                    if (x)
                    {
                        ConnectionModel.ResumeUpdates();
                    }
                    else
                    {
                        ConnectionModel.PauseUpdates();
                    }
                }).DisposeWith(disposables);

                // Tell model we want to monitor the calls as dictated by the workspace
                workspace.MonitoredCalls
                    .Transform(call => call.Call.InstrumentedCallId)
                    .OnItemAdded(ConnectionModel.StartMonitoringCall)
                    .OnItemRemoved(ConnectionModel.StopMonitoringCall)
                    .Subscribe()
                    .DisposeWith(disposables);

                attachGoPauseHandlers(commandHandlerService).DisposeWith(disposables);

                var closeCommand = ReactiveUI.ReactiveCommand.Create(() => connectionService.Close());
                commandHandlerService.RegisterHandler(Commands.CloseWorkspace, closeCommand).DisposeWith(disposables);

                // Document screens
                Observable.Empty<IWorkspaceDocumentScreen>()
                    .StartWith(configScreen)
                    .ToObservableChangeSet()
                    .OnItemAdded(s =>
                    {
                        s.Model = ConnectionModel.Model;
                        s.Workspace = workspace;
                    })
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var documentScreens)
                    .Subscribe()
                    .DisposeWith(disposables);

                DocumentScreens = documentScreens;
            });
        }

        public IConnectionModel ConnectionModel { get; set; }

        public ReadOnlyObservableCollection<IWorkspaceDocumentScreen> DocumentScreens { get; private set; }

        private IWorkspaceDocumentScreen mActiveDocumentScreen;
        public IWorkspaceDocumentScreen ActiveDocumentScreen
        {
            get => mActiveDocumentScreen;
            set => Set(ref mActiveDocumentScreen, value);
        }
    }
}
