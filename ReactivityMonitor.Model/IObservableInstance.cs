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
        IObservable<IChangeSet<IObservableInstance>> Inputs { get; }
    }
}
