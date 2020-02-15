using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewInstrumentedCall
    {
        public NewInstrumentedCall(int id, string calledMethod, int instructionOffset)
        {
            Id = id;
            CalledMethod = calledMethod;
            InstructionOffset = instructionOffset;
        }

        public int Id { get; }
        public string CalledMethod { get; }
        public int InstructionOffset { get; }
    }
}
