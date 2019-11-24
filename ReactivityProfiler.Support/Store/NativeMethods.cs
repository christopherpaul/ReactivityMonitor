using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal static class NativeMethods
    {
        [DllImport("ReactivityProfiler.dll")]
        public extern static int GetStoreLength();

        [DllImport("ReactivityProfiler.dll")]
        public extern static int ReadStore(
            int offset,
            byte[] buffer,
            int byteCount);
    }
}
