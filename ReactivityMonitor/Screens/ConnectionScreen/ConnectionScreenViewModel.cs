using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;

namespace ReactivityMonitor.Screens.ConnectionScreen
{
    public sealed class ConnectionScreenViewModel : ReactiveScreen, IConnectionScreenViewModel
    {
        private readonly ServerDiscovery mServerDiscovery;

        public ConnectionScreenViewModel()
        {
            mServerDiscovery = new ServerDiscovery();

            this.WhenActivated(observables =>
            {
                var changeSet = ObservableChangeSet.Create<Server, int>(list =>
                {
                    return Observable.Interval(TimeSpan.FromSeconds(1))
                        .StartWith(0)
                        .Select(_ => mServerDiscovery.Scan())
                        .Subscribe(servers => list.EditDiff(servers, (s1, s2) => s1.ProcessId == s2.ProcessId));
                }, server => server.ProcessId);

                changeSet
                    .ObserveOnDispatcher()
                    .Bind(out mAvailableConnections)
                    .Subscribe()
                    .DisposeWith(observables);
            });
        }

        private ReadOnlyObservableCollection<Server> mAvailableConnections;
        public ReadOnlyObservableCollection<Server> AvailableConnections => mAvailableConnections;
    }
}
