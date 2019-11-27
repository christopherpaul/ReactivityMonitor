using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class MethodCallInstrumentedEvent
    {
        public int InstrumentationPointId { get; set; }
        public ulong ModuleId { get; set; }
        public uint FunctionToken { get; set; }
        public int InstructionOffset { get; set; }
        public string OwningTypeName { get; set; }
        public string CallingMethodName { get; set; }
        public string CalledMethodName { get; set; }
    }
}
