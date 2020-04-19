using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class InstrumentedMethod : IInstrumentedMethod
    {
        public InstrumentedMethod(int id, IModule module, uint metadataToken, string detailName, ISourceMethod sourceMethod)
        {
            InstrumentedMethodId = id;
            Module = module;
            MetadataToken = metadataToken;
            DetailName = detailName;
            SourceMethod = sourceMethod;
        }

        public int InstrumentedMethodId { get; }
        public IModule Module { get; }
        public uint MetadataToken { get; }
        public string DetailName { get; }
        public ISourceMethod SourceMethod { get; }
        public IImmutableList<IInstrumentedCall> InstrumentedCalls { get; internal set; }
    }
}
