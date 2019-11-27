using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
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

        public void Open(Server server)
        {
            mConnectionSubject.OnNext(new ConnectionModel(server));
        }

        public void Close()
        {
            mConnectionSubject.OnNext(null);
        }
    }
}
