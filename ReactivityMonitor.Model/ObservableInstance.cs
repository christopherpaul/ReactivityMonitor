using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using DynamicData;

namespace ReactivityMonitor.Model
{
    internal sealed class ObservableInstance : IObservableInstance
    {
        public ObservableInstance(
            EventInfo created, 
            IObservable<IInstrumentedCall> call, 
            IObservable<IObservableInstance> inputs,
            IObservable<ISubscription> subscriptions)
        {
            Created = created;
            call.Subscribe(c => Call = c);
            Inputs = inputs;
            Subscriptions = subscriptions;
        }

        public EventInfo Created { get; }
        public IInstrumentedCall Call { get; private set; }
        public long ObservableId => Created.SequenceId;

        public IObservable<IObservableInstance> Inputs { get; }
        public IObservable<ISubscription> Subscriptions { get; }
    }
}
