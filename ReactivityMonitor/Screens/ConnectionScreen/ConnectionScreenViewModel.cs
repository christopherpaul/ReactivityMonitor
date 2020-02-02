using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using ReactivityMonitor.ProfilerClient;
using ReactivityMonitor.Services;

namespace ReactivityMonitor.Screens.ConnectionScreen
{
    public sealed class ConnectionScreenViewModel : ReactiveScreen, IConnectionScreen
    {
        private readonly IConnectionService mConnectionService;
        private readonly IDialogService mDialogService;

        public ConnectionScreenViewModel(IConnectionService connectionService, IDialogService dialogService)
        {
            mConnectionService = connectionService;
            mDialogService = dialogService;

            DisplayName = "Processes";

            OpenSelectedServer = ReactiveCommand.CreateFromTask(
                execute: () => connectionService.Open(SelectedServer),
                canExecute: mSelectedServer.Select(s => s != null));

            BrowseAndLaunch = ReactiveCommand.CreateFromTask(ExecuteBrowseAndLaunch);

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
        public ICommand BrowseAndLaunch { get; }

        private async Task ExecuteBrowseAndLaunch()
        {
            string filename = await mDialogService.ShowOpenFileDialog("Start process", $"Supported files|*.exe;*{DataFile.ProfileDataFileExtension}|Programs|*.exe|Data files|*{DataFile.ProfileDataFileExtension}|All files|*.*").ConfigureAwait(false);
            if (filename == null)
            {
                return;
            }

            if (string.Equals(Path.GetExtension(filename), DataFile.ProfileDataFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                await mConnectionService.OpenDataFile(filename);
                return;
            }

            var launchInfo = new LaunchInfo
            {
                FileName = filename
            };
            try
            {
                await mConnectionService.Launch(launchInfo).ConfigureAwait(false);
            }
            catch (ConnectionException ex)
            {
                await mDialogService.ShowErrorDialog("Start process", ex.Message).ConfigureAwait(false);
            }
        }
    }
}
