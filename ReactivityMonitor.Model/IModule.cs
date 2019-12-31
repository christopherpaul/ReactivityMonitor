using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IModule
    {
        ulong ModuleId { get; }
        string Path { get; }

        IObservable<IInstrumentedCall> InstrumentedCalls { get; }
    }
}
