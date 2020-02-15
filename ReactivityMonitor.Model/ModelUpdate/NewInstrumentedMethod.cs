using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewInstrumentedMethod
    {
        public NewInstrumentedMethod(int id, ulong moduleId, uint metadataToken, string owningType, string name, IEnumerable<NewInstrumentedCall> instrumentedCalls)
        {
            Id = id;
            ModuleId = moduleId;
            MetadataToken = metadataToken;
            OwningType = owningType;
            Name = name;
            InstrumentedCalls = instrumentedCalls;
        }

        public int Id { get; }
        public ulong ModuleId { get; }
        public uint MetadataToken { get; }
        public string OwningType { get; }
        public string Name { get; }
        public IEnumerable<NewInstrumentedCall> InstrumentedCalls { get; }
    }
}
