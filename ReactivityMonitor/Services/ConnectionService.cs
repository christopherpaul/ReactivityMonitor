using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Utility.Flyweights;

namespace ReactivityMonitor.Services
{
    public sealed class ConnectionService : IConnectionService, IDisposable
    {
        private readonly ServerDiscovery mServerDiscovery;
        private readonly ISubject<IConnectionModel> mConnectionSubject;
        private readonly string mProfilerLocation32;
        private readonly string mProfilerLocation64;
        private readonly string mSupportAssemblyLocation;
        private readonly SerialDisposable mActiveConnection = new SerialDisposable();

        public ConnectionService()
        {
            mServerDiscovery = new ServerDiscovery();

            AvailableServers = ObservableChangeSet.Create<Server, int>(list =>
            {
                return Observable.Interval(TimeSpan.FromSeconds(1))
                    .StartWith(0)
                    .Select(_ => mServerDiscovery.Scan())
                    .Subscribe(servers => list.EditDiff(servers, (s1, s2) => s1.ProcessId == s2.ProcessId));
            }, server => server.ProcessId);

            mConnectionSubject = new BehaviorSubject<IConnectionModel>(null);

            var profilersLocation =
                Path.Combine(
                    Path.GetDirectoryName(
                        new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath),
                    "profiler");

            const string cProfilerDll = "ReactivityProfiler.dll";
            mProfilerLocation32 = Path.Combine(profilersLocation, "Win32", cProfilerDll);
            mProfilerLocation64 = Path.Combine(profilersLocation, "x64", cProfilerDll);
            mSupportAssemblyLocation = Path.Combine(profilersLocation, "x64", "ReactivityProfiler.Support.dll");
        }

        public IObservable<IChangeSet<Server, int>> AvailableServers { get; }

        public IObservable<IConnectionModel> WhenConnectionChanges => mConnectionSubject.AsObservable();

        public async Task Launch(LaunchInfo launchInfo, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = launchInfo.FileName;
            psi.Arguments = launchInfo.Arguments;

            psi.UseShellExecute = false;

            foreach (string prefix in new[] { "COR", "CORECLR" })
            {
                psi.Environment.Add($"{prefix}_ENABLE_PROFILING", "1");
                psi.Environment.Add($"{prefix}_PROFILER_PATH_32", mProfilerLocation32);
                psi.Environment.Add($"{prefix}_PROFILER_PATH_64", mProfilerLocation64);
                psi.Environment.Add($"{prefix}_PROFILER", "{09c5b5d7-62d2-4448-911d-2e1346a21110}");
            }

            psi.Environment.Add("DOTNET_STARTUP_HOOKS", mSupportAssemblyLocation);

            string pipeName = $"ReactivityProfiler.{Path.GetFileNameWithoutExtension(launchInfo.FileName)}.{Guid.NewGuid():N}";
            psi.Environment.Add("REACTIVITYPROFILER_PIPENAME", pipeName);

            Process process;
            try
            {
                process = Process.Start(psi);
            }
            catch (Exception ex)
            {
                throw new ConnectionException($"Failed to start process: {ex.Message}");
            }

            if (process == null)
            {
                throw new ConnectionException("Process failed to start.");
            }

            var server = new Server(process.Id, process.ProcessName, pipeName);

            await Open(server, cancellationToken).ConfigureAwait(false);
        }

        public async Task Open(Server server, CancellationToken cancellationToken)
        {
            var process = Process.GetProcessById(server.ProcessId);
            var whenProcessExits = Observable.FromEventPattern(h => process.Exited += h, h => process.Exited -= h);
            process.EnableRaisingEvents = true;
            if (process.HasExited)
            {
                ThrowProcessExitedError();
            }

            var connectionModel = new ConnectionModel(server);
            mActiveConnection.Disposable = connectionModel.Connect();

            bool connected = await connectionModel.WhenConnected.Where(isConnected => isConnected)
                .Merge(whenProcessExits.Select(_ => false))
                .Take(1)
                .ToTask(cancellationToken);

            if (!connected)
            {
                ThrowProcessExitedError();
            }

            mConnectionSubject.OnNext(connectionModel);

            void ThrowProcessExitedError()
            {
                throw new ConnectionException($"Process exited before communication was established.");
            }
        }

        public void Close()
        {
            mActiveConnection.Disposable = null;
            mConnectionSubject.OnNext(null);
        }

        public void Dispose()
        {
            mActiveConnection.Dispose();
        }
    }
}
