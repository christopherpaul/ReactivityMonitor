using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData;

namespace ReactivityMonitor.Workspace
{
    internal class MonitoringGroup : IMonitoringGroup
    {
        private readonly ISubject<string> mNameSubject;
        private readonly ISourceCache<int, int> mCallIds;

        public MonitoringGroup(string name, IObservableCache<IMonitoredCall, int> monitoredCalls)
        {
            mNameSubject = new BehaviorSubject<string>(name ?? throw new ArgumentNullException(nameof(name)));
            mCallIds = new SourceCache<int, int>(id => id);

            WhenNameChanges = mNameSubject.AsObservable();
            Calls = monitoredCalls.Connect()
                .InnerJoin(mCallIds.Connect(), id => id, (call, _) => call)
                .RemoveKey();
        }

        public IObservable<string> WhenNameChanges { get; }

        public IObservable<IChangeSet<IMonitoredCall>> Calls { get; }

        public void SetName(string name)
        {
            mNameSubject.OnNext(name ?? throw new ArgumentNullException(nameof(name)));
        }

        public void AddCall(IMonitoredCall call)
        {
            mCallIds.AddOrUpdate(call.Call.InstrumentedCallId);
        }

        public void RemoveCall(IMonitoredCall call)
        {
            mCallIds.RemoveKey(call.Call.InstrumentedCallId);
        }
    }
}
