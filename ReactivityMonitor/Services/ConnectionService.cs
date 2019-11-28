using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
        }

        public IObservable<IChangeSet<Server, int>> AvailableServers { get; }

        public IObservable<IConnectionModel> WhenConnectionChanges => mConnectionSubject.AsObservable();

        public async Task Launch(LaunchInfo launchInfo, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = launchInfo.FileName;
            psi.Arguments = launchInfo.Arguments;

            psi.UseShellExecute = false;
            psi.Environment.Add("COR_ENABLE_PROFILING", "1");
            psi.Environment.Add("COR_PROFILER", "ReactivityMonitor.RxProfiler.1");

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
