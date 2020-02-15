using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class MethodDoneEvent
    {
        public int InstrumentedMethodId { get; set; }
    }
}
