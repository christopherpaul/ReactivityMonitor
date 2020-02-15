using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IInstrumentedMethod
    {
        int InstrumentedMethodId { get; }
        IModule Module { get; }
        uint MetadataToken { get; }
        string ParentType { get; }
        string Name { get; }

        IImmutableList<IInstrumentedCall> InstrumentedCalls { get; }
    }
}
