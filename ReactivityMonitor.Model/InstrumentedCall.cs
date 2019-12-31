using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class InstrumentedCall : IInstrumentedCall
    {
        public InstrumentedCall(int id, IObservable<IModule> module, string callingType, string callingMethod, string calledMethod, int instructionOffset, IObservable<IObservableInstance> observableInstances)
        {
            InstrumentedCallId = id;
            module.Subscribe(m => Module = m);
            CallingType = callingType;
            CallingMethod = callingMethod;
            CalledMethod = calledMethod;
            InstructionOffset = instructionOffset;
            ObservableInstances = observableInstances;
        }

        public int InstrumentedCallId { get; }

        public IModule Module { get; private set; }

        public string CallingType { get; }

        public string CallingMethod { get; }

        public string CalledMethod { get; }

        public int InstructionOffset { get; }

        public IObservable<IObservableInstance> ObservableInstances { get; }
    }
}
