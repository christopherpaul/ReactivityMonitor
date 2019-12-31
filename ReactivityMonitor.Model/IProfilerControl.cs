using DynamicData;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IProfilerControl
    {
        IObservable<IChangeSet<int, int>> RequestedInstrumentedCallIds { get; } // key and value are both set to the ID
    }
}
