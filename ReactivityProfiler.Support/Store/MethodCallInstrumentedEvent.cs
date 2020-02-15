using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class MethodCallInstrumentedEvent
    {
        public int InstrumentationPointId { get; set; }
        public int InstrumentedMethodId { get; set; }
        public int InstructionOffset { get; set; }
        public string CalledMethodName { get; set; }
    }
}
