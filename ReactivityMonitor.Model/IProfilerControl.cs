using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IProfilerControl
    {
        IObservableList<int> RequestedInstrumentedCallIds { get; }
    }
}
