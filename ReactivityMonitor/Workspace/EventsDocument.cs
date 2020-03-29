using DynamicData;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Workspace
{
    internal sealed class EventsDocument : IEventsDocument
    {
        private readonly ISourceCache<IInstrumentedCall, int> mCallsSource;
        private readonly ISourceCache<IObservableInstance, long> mObservablesSource;

        public EventsDocument(IWorkspace workspace)
        {
            Workspace = workspace;

            mCallsSource = new SourceCache<IInstrumentedCall, int>(c => c.InstrumentedCallId);
            mObservablesSource = new SourceCache<IObservableInstance, long>(obs => obs.ObservableId);

            Calls = mCallsSource.Connect();
            Observables = mObservablesSource.Connect();
        }

        public IObservable<IChangeSet<IInstrumentedCall, int>> Calls { get; }
        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; }
        public IWorkspace Workspace { get; }
        public string DocumentName { get; set; }

        public void AddRange(IEnumerable<IInstrumentedCall> calls) => mCallsSource.AddOrUpdate(calls);
        public void AddRange(IEnumerable<IObservableInstance> observables) => mObservablesSource.AddOrUpdate(observables);
    }
}
