using Caliburn.Micro;
using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Screens.CallsScreen;
using ReactivityMonitor.Screens.EventListScreen;
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
            ICallsScreen callsScreen, 
            IScreenFactory screenFactory,
            IEventListScreen eventListScreen,
            IPayloadScreen payloadScreen,
            IConcurrencyService concurrencyService,
            ICommandHandlerService commandHandlerService)
        {
            var isUpdating = GoPauseControl.SetupGoPause(out var attachGoPauseHandlers)
                .ObserveOn(concurrencyService.TaskPoolRxScheduler);

            Calls = callsScreen;
            callsScreen.ConductWith(this);

            EventList = eventListScreen;
            eventListScreen.WhenActiveMonitoringGroupChanges = this.WhenAnyValue(x => x.ActiveMonitoringScreen).Select(s => s.MonitoringGroup);
            eventListScreen.ConductWith(this);

            PayloadScreen = payloadScreen;

            WhenActivated(disposables =>
            {
                DisplayName = ConnectionModel.Server.ProcessName;

                callsScreen.Model = ConnectionModel.Model;
                callsScreen.Workspace = workspace;

                eventListScreen.Model = ConnectionModel.Model;

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

                workspace.MonitoringGroups
                    .Transform(grp =>
                    {
                        var monitoringScreen = screenFactory.CreateMonitoringScreen();
                        monitoringScreen.Model = ConnectionModel.Model;
                        monitoringScreen.Workspace = workspace;
                        monitoringScreen.MonitoringGroup = grp;
                        return monitoringScreen;
                    })
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var monitoringGroups)
                    .OnItemAdded(newMonitoringScreen => ActiveMonitoringScreen = newMonitoringScreen)
                    .Subscribe()
                    .DisposeWith(disposables);

                MonitoringScreens = monitoringGroups;

                attachGoPauseHandlers(commandHandlerService).DisposeWith(disposables);

                PayloadScreen.ConnectionModel = ConnectionModel;
                PayloadScreen.Activator.Activate().DisposeWith(disposables);
            });
        }

        public IConnectionModel ConnectionModel { get; set; }

        public ICallsScreen Calls { get; }
        public IEventListScreen EventList { get; }
        public IPayloadScreen PayloadScreen { get; }

        public ReadOnlyObservableCollection<IMonitoringScreen> MonitoringScreens { get; private set; }

        private IMonitoringScreen mActiveMonitoringScreen;
        public IMonitoringScreen ActiveMonitoringScreen
        {
            get => mActiveMonitoringScreen;
            set => Set(ref mActiveMonitoringScreen, value);
        }
    }
}
