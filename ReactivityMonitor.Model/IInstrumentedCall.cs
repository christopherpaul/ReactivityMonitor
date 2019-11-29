﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IInstrumentedCall
    {
        int InstrumentedCallId { get; }
        IModule Module { get; }
        string CallingType { get; }
        string CallingMethod { get; }
        string CalledMethod { get; }
        int InstructionOffset { get; }
    }
}