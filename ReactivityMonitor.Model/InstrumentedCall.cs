using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class InstrumentedCall : IInstrumentedCall
    {
        public InstrumentedCall(int id, IInstrumentedMethod method, string calledMethod, int instructionOffset, IObservable<IObservableInstance> observableInstances)
        {
            InstrumentedCallId = id;
            Method = method;
            CalledMethod = calledMethod;
            InstructionOffset = instructionOffset;
            ObservableInstances = observableInstances;
        }

        public int InstrumentedCallId { get; }
        public IInstrumentedMethod Method { get; }
        public string CalledMethod { get; }

        public int InstructionOffset { get; }

        public IObservable<IObservableInstance> ObservableInstances { get; }
    }
}
