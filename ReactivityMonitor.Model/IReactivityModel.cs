using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IReactivityModel
    {
        IObservableCache<IModule, ulong> Modules { get; }

        IObservableCache<IInstrumentedCall, int> InstrumentedCalls { get; }
    }
}
