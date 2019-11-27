using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Model;

namespace ReactivityMonitor.Connection
{
    public sealed class ConnectionModel : IConnectionModel
    {
        private readonly ProfilerClient.Client mProfilerClient;

        public ConnectionModel(Server server)
        {
            Server = server;

            var modelSource = new ReactivityModelSource();
            Model = modelSource.Model;

            mProfilerClient = new ProfilerClient.Client(
                server.PipeName,
                modelSource.Updater,
                modelSource.ProfilerControl);
        }

        public Server Server { get; }

        public IReactivityModel Model { get; }

        public IDisposable Connect()
        {
            return mProfilerClient.Connect();
        }
    }
}
