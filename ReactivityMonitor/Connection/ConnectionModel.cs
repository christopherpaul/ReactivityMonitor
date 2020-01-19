using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using ReactivityMonitor.Model;
using ReactivityMonitor.ProfilerClient;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Connection
{
    public sealed class ConnectionModel : IConnectionModel
    {
        private readonly ISourceCache<int, int> mMonitoredCalls;
        private readonly Action<ObjectDataRequest> mMakeObjectDataRequest;
        private readonly Client mClient;

        public ConnectionModel(Server server)
        {
            Server = server;

            mMonitoredCalls = new SourceCache<int, int>(c => c);

            var objectDataRequestSubject = Subject.Synchronize(new Subject<ObjectDataRequest>());
            mMakeObjectDataRequest = objectDataRequestSubject.OnNext;

            var profilerControl = new ProfilerControl(mMonitoredCalls.Connect(), objectDataRequestSubject);

            mClient = Client.Create(server.PipeName, profilerControl);

            Model = ReactivityModel.Create(mClient.ModelUpdateSource);
        }

        public Server Server { get; }

        public IReactivityModel Model { get; }

        public IDisposable Connect()
        {
            return mClient.Connect();
        }

        public IObservable<bool> WhenConnected => mClient.WhenConnected;

        public void PauseUpdates()
        {
            mClient.ModelUpdateSource.Pause();
        }

        public void ResumeUpdates()
        {
            mClient.ModelUpdateSource.Resume();
        }

        public void StartMonitoringCall(int callId)
        {
            mMonitoredCalls.AddOrUpdate(callId);
        }

        public void StopMonitoringCall(int callId)
        {
            mMonitoredCalls.RemoveKey(callId);
        }

        public void RequestObjectProperties(long objectId)
        {
            mMakeObjectDataRequest(new ObjectDataRequest(objectId));
        }

        private sealed class ProfilerControl : IProfilerControl
        {
            public ProfilerControl(IObservable<IChangeSet<int, int>> requestedInstrumentedCallIds,
                IObservable<ObjectDataRequest> objectDataRequests)
            {
                RequestedInstrumentedCallIds = requestedInstrumentedCallIds;
                ObjectDataRequests = objectDataRequests;
            }

            public IObservable<IChangeSet<int, int>> RequestedInstrumentedCallIds { get; }
            public IObservable<ObjectDataRequest> ObjectDataRequests { get; }
        }
    }
}
