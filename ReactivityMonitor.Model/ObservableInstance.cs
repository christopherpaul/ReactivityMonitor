using System;
using System.Collections.Generic;
using System.Text;
using DynamicData;

namespace ReactivityMonitor.Model
{
    internal sealed class ObservableInstance : IObservableInstance
    {
        private static readonly Func<IObservableInstance, long> cKeyFunc = obs => obs.ObservableId;
        private readonly ISourceCache<IObservableInstance, long> mInputs;

        public ObservableInstance(EventInfo created, IInstrumentedCall call, IObservable<IChangeSet<IObservableInstance>> inputs)
        {
            Created = created;
            Call = call;
            Inputs = inputs;
        }

        public EventInfo Created { get; }
        public IInstrumentedCall Call { get; }
        public long ObservableId => Created.SequenceId;

        public IObservable<IChangeSet<IObservableInstance>> Inputs { get; }
    }
}
