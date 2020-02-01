using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IReactivityModel
    {
        IObservable<IModule> Modules { get; }

        IObservable<IInstrumentedCall> InstrumentedCalls { get; }

        IObservable<IObservableInstance> ObservableInstances { get; }

        IObservable<ClientEvent> ClientEvents { get; }
    }
}
