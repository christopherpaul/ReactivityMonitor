using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class InstrumentedMethod : IInstrumentedMethod
    {
        public InstrumentedMethod(int id, IModule module, uint metadataToken, string parentType, string name)
        {
            InstrumentedMethodId = id;
            Module = module;
            MetadataToken = metadataToken;
            ParentType = parentType;
            Name = name;
        }

        public int InstrumentedMethodId { get; }
        public IModule Module { get; }
        public uint MetadataToken { get; }
        public string ParentType { get; }
        public string Name { get; }
        public IImmutableList<IInstrumentedCall> InstrumentedCalls { get; internal set; }
    }
}
