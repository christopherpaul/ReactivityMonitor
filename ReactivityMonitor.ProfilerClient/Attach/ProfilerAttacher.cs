using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ReactivityMonitor.ProfilerClient.Attach
{
    internal static class ProfilerAttacher
    {
        /// <summary>
        /// Loads the specified profiler into the process 'processID'.   
        /// </summary>
        public static bool AttachToProcess(int processID, Guid profilerGuid, string profilerPath, byte[] data)
        {
            Trace.WriteLine("Loading the profiler.");
            Trace.WriteLine("Turning on debug privilege.");
            SetDebugPrivilege();

            CLRMetaHost mh = new CLRMetaHost();
            CLRRuntimeInfo highestLoadedRuntime = null;
            foreach (CLRRuntimeInfo runtime in mh.EnumerateLoadedRuntimes(processID))
            {
                if (highestLoadedRuntime == null ||
                    string.Compare(highestLoadedRuntime.GetVersionString(), runtime.GetVersionString(), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    highestLoadedRuntime = runtime;
                }
            }
            if (highestLoadedRuntime == null)
            {
                Trace.WriteLine("Could not enumerate .NET runtimes on the system.");
                return false;
            }

            var version = highestLoadedRuntime.GetVersionString();
            Trace.WriteLine("Highest Runtime in process is version {0}", version);
            if (version.StartsWith("v2"))
            {
                throw new ApplicationException("Object logging only supported on V4.0 .NET runtimes.");
            }

            ICLRProfiling clrProfiler = highestLoadedRuntime.GetProfilingInterface();
            if (clrProfiler == null)
            {
                throw new ApplicationException("Could not get Attach Profiler interface (target runtime must be at least V4.0))");
            }

            // Warn the user to unsuspend win8 apps if 3 seconds goes by 
            bool attached = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(3000);
                if (!attached)
                {
                    Trace.WriteLine("[Can't Attach Yet... Bring Win8 Apps to the forground.]");
                    Trace.Flush();
                }
            });

            try
            {
                // Wait 30 seconds because you may have to wake the process for win8 
                Trace.WriteLine("Trying to attach profiler.");
                // We use the provider guid as the GUID of the COM object for the profiler too. 
                int ret = clrProfiler.AttachProfiler(processID, 30000, profilerGuid, profilerPath, data, data?.Length ?? 0);
                attached = true;
                Trace.WriteLine(string.Format("Done attaching profiler ret = {0}", ret));
            }
            catch (COMException e)
            {
                if (e.ErrorCode == unchecked((int)0x800705B4))  // Timeout
                {
                    throw new ApplicationException("Timeout: For Win8 Apps this may because they were suspended.  Make sure to switch to the app.");
                }
                // TODO Confirm this error code is what I think it is. 
                if (e.ErrorCode == unchecked((int)0x8013136a))
                {
                    throw new ApplicationException("A CLR Profiler has already been attached.  You cannot attach another. (a process restart will fix)");
                }

                Trace.WriteLine("Failure attaching profiler, see the Windows Application Event Log for details.");
                throw;
            }

            Trace.WriteLine("Attached profiler.");
            return true;
        }

        private static void SetDebugPrivilege()
        {
            Native.SetPrivilege(Native.SE_DEBUG_PRIVILEGE);
        }
    }
}
