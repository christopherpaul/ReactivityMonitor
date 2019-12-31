using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewInstrumentedCall
    {
        public NewInstrumentedCall(int id, ulong moduleId, string callingType, string callingMethod, string calledMethod, int instructionOffset)
        {
            Id = id;
            ModuleId = moduleId;
            CallingType = callingType;
            CallingMethod = callingMethod;
            CalledMethod = calledMethod;
            InstructionOffset = instructionOffset;
        }

        public int Id { get; }
        public ulong ModuleId { get; }
        public string CallingType { get; }
        public string CallingMethod { get; }
        public string CalledMethod { get; }
        public int InstructionOffset { get; }
    }
}
