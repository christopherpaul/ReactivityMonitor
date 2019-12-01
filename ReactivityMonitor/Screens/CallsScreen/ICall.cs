using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.CallsScreen
{
    public interface ICall
    {
        string CalledMethodName { get; }
        int InstructionOffset { get; }
    }
}
