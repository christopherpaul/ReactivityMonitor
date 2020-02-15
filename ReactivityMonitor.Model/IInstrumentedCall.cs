using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IInstrumentedCall
    {
        int InstrumentedCallId { get; }
        IInstrumentedMethod Method { get; }
        string CalledMethod { get; }
        int InstructionOffset { get; }

        IObservable<IObservableInstance> ObservableInstances { get; }
    }
}
