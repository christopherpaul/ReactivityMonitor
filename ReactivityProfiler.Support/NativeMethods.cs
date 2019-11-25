using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReactivityProfiler.Support
{
    internal static class NativeMethods
    {
        [DllImport("ReactivityProfiler.dll")]
        public extern static int GetStoreEventCount();

        [DllImport("ReactivityProfiler.dll")]
        private extern static void ReadStoreEvent(int index, ref IntPtr pEventData, ref int byteCount);

        public static byte[] ReadStoreEvent(int index)
        {
            IntPtr pEventData = default;
            int byteCount = default;
            ReadStoreEvent(index, ref pEventData, ref byteCount);

            byte[] buffer = new byte[byteCount];
            Marshal.Copy(pEventData, buffer, 0, byteCount);

            return buffer;
        }

        [DllImport("ReactivityProfiler.dll", CharSet = CharSet.Unicode)]
        public extern static void SetChannelPipeName(string pipeName);
    }
}
