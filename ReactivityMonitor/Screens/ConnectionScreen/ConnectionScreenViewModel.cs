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

            DisplayName = "Welcome";

            BrowseExecutableCommand = ReactiveCommand.CreateFromTask(BrowseExecutableAsync);
            LaunchCommand = ReactiveCommand.CreateFromTask(
                LaunchExecutableAsync, 
                this.WhenAnyValue(x => x.LaunchExecutablePath).Select(path => !string.IsNullOrWhiteSpace(path)));
            BrowseDataFileCommand = ReactiveCommand.CreateFromTask(BrowseDataFileAsync);

            WhenActivated(observables =>
            {
            });
        }

        private BehaviorSubject<string> mLaunchExecutablePath = new BehaviorSubject<string>(string.Empty);
        public string LaunchExecutablePath
        {
            get => mLaunchExecutablePath.Value;
            set => Set(mLaunchExecutablePath, value);
        }

        private BehaviorSubject<string> mLaunchArguments = new BehaviorSubject<string>(string.Empty);
        public string LaunchArguments
        {
            get => mLaunchExecutablePath.Value;
            set => Set(mLaunchArguments, value);
        }

        private BehaviorSubject<bool> mMonitorAllOnLaunch = new BehaviorSubject<bool>(false);
        public bool MonitorAllOnLaunch
        {
            get => mMonitorAllOnLaunch.Value;
            set => Set(mMonitorAllOnLaunch, value);
        }

        public ICommand LaunchCommand { get; }
        public ICommand BrowseExecutableCommand { get; }
        public ICommand BrowseDataFileCommand { get; }

        private async Task BrowseExecutableAsync()
        {
            string filename = await mDialogService.ShowOpenFileDialog("Start process", $"Programs|*.exe|All files|*.*");
            if (filename == null)
            {
                return;
            }

            LaunchExecutablePath = filename;
        }

        private async Task LaunchExecutableAsync()
        {
            string filename = LaunchExecutablePath;
            if (string.IsNullOrWhiteSpace(filename))
            {
                await mDialogService.ShowErrorDialog("Start process", "Please enter the path of an executable to launch");
                return;
            }

            var launchInfo = new LaunchInfo
            {
                FileName = filename,
                Arguments = LaunchArguments,
                Options = MonitorAllOnLaunch ? LaunchOptions.MonitorAllFromStart : LaunchOptions.Default
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

        private async Task BrowseDataFileAsync()
        {
            string filename = await mDialogService.ShowOpenFileDialog("Start process", $"Data files|*{DataFile.ProfileDataFileExtension}|All files|*.*");
            if (filename == null)
            {
                return;
            }

            await mConnectionService.OpenDataFile(filename);
        }
    }
}
