using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IObservableInstance
    {
        EventInfo Created { get; }
        long ObservableId { get; }
        IInstrumentedCall Call { get; }
        IObservable<IObservableInstance> Inputs { get; }

        /// <summary>
        /// All subscriptions that (have) been made to this observable instance. This is a
        /// cold observable.
        /// </summary>
        IObservable<ISubscription> Subscriptions { get; }
    }
}
