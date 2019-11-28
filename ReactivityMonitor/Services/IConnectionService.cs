using DynamicData;
using ReactivityMonitor.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public interface IConnectionService
    {
        IObservable<IChangeSet<Server, int>> AvailableServers { get; }

        IObservable<IConnectionModel> WhenConnectionChanges { get; }

        Task Launch(LaunchInfo launchInfo, CancellationToken cancellationToken = default);
        Task Open(Server server, CancellationToken cancellation = default);
        void Close();
    }
}
