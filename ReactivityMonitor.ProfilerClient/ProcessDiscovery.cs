using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ReactivityMonitor.ProfilerClient
{
    public static class ProcessDiscovery
    {
        public static IEnumerable<Process> GetAttachableProcesses()
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            return Process.GetProcesses()
                .Where(p =>
                {
                    if (p.Id == currentProcessId)
                    {
                        return false;
                    }

                    try
                    {
                        return p.Modules.Cast<ProcessModule>().Any(m => m.ModuleName.StartsWith("mscor", StringComparison.OrdinalIgnoreCase));
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                })
                .ToList();
        }
    }
}
