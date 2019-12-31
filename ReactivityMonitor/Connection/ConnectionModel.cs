using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using ReactivityMonitor.Model;
using ReactivityMonitor.ProfilerClient;

namespace ReactivityMonitor.Connection
{
    public sealed class ConnectionModel : IConnectionModel
    {
        private readonly ISourceCache<int, int> mMonitoredCalls;
        private readonly IModelUpdateSource mModelUpdates;

        public ConnectionModel(Server server)
        {
            Server = server;

            mMonitoredCalls = new SourceCache<int, int>(c => c);
            var profilerControl = new ProfilerControl(mMonitoredCalls.Connect());

            mModelUpdates = Client.CreateModelUpdateSource(
                server.PipeName,
                profilerControl);

            Model = ReactivityModel.Create(mModelUpdates);
        }

        public Server Server { get; }

        public IReactivityModel Model { get; }

        public IDisposable Connect()
        {
            return mModelUpdates.Connect();
        }

        public void StartMonitoringCall(int callId)
        {
            mMonitoredCalls.AddOrUpdate(callId);
        }

        public void StopMonitoringCall(int callId)
        {
            mMonitoredCalls.RemoveKey(callId);
        }

        private sealed class ProfilerControl : IProfilerControl
        {
            public ProfilerControl(IObservable<IChangeSet<int, int>> requestedInstrumentedCallIds)
            {
                RequestedInstrumentedCallIds = requestedInstrumentedCallIds;
            }

            public IObservable<IChangeSet<int, int>> RequestedInstrumentedCallIds { get; }
        }
    }
}
