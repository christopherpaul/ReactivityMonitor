using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support
{
    internal static class Registry
    {
        public static void SetChannelPipeName(string pipeName)
        {
            NativeMethods.SetChannelPipeName(pipeName);
        }
    }
}
