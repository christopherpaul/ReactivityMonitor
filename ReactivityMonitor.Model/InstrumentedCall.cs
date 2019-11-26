using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class InstrumentedCall : IInstrumentedCall
    {
        public InstrumentedCall(int id, IModule module)
        {
            InstrumentedCallId = id;
            Module = module;
        }

        public int InstrumentedCallId { get; }

        public IModule Module { get; }
    }
}
