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
using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Services;

namespace ReactivityMonitor.Screens.ConnectionScreen
{
    public sealed class ConnectionScreenViewModel : ReactiveScreen, IConnectionScreen
    {
        public ConnectionScreenViewModel(IConnectionService connectionService)
        {
            DisplayName = "Processes";

            OpenSelectedServer = ReactiveCommand.Create(
                execute: () => connectionService.Open(SelectedServer),
                canExecute: mSelectedServer.Select(s => s != null));

            WhenActivated(observables =>
            {
                connectionService.AvailableServers
                    .ObserveOnDispatcher()
                    .Bind(out mAvailableConnections)
                    .Subscribe()
                    .DisposeWith(observables);
            });
        }

        private ReadOnlyObservableCollection<Server> mAvailableConnections;
        public ReadOnlyObservableCollection<Server> AvailableConnections => mAvailableConnections;

        private BehaviorSubject<Server> mSelectedServer = new BehaviorSubject<Server>(null);
        public Server SelectedServer
        {
            get => mSelectedServer.Value;
            set => Set(mSelectedServer, value);
        }

        public ICommand OpenSelectedServer { get; }
    }
}
