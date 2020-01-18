using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using ReactivityMonitor.Connection;

namespace ReactivityMonitor.Services
{
    public sealed class ConnectionService : IConnectionService
    {
        private readonly ServerDiscovery mServerDiscovery;
        private readonly ISubject<IConnectionModel> mConnectionSubject;
        private readonly string mProfilerLocation32;
        private readonly string mProfilerLocation64;

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

            Server server;
            while (!mServerDiscovery.TryGetServer(process, out server))
            {
                if (process.HasExited)
                {
                    throw new ConnectionException($"Process exited before connection was established (with exit code {process.ExitCode}).");
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }

            await Open(server, cancellationToken).ConfigureAwait(false);
        }

        public Task Open(Server server, CancellationToken cancellationToken)
        {
            mConnectionSubject.OnNext(new ConnectionModel(server));
            return Task.CompletedTask;
        }

        public void Close()
        {
            mConnectionSubject.OnNext(null);
        }
    }
}
