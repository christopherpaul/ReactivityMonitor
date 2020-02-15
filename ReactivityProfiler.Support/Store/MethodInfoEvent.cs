using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class MethodInfoEvent
    {
        public int InstrumentedMethodId { get; set; }
        public ulong ModuleId { get; set; }
        public uint FunctionToken { get; set; }
        public string OwningTypeName { get; set; }
        public string Name { get; set; }
    }
}
