using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Connection
{
    internal sealed class ServerDiscovery
    {
        private const string cSoftware = "Software";
        private const string cProduct = "RxProfiler";
        private const string cServers = "Servers";
        private const string cPipeName = "PipeName";

        public IReadOnlyList<Server> Scan()
        {
            var list = new List<Server>();
            try
            {
                using (var software = Registry.CurrentUser.OpenSubKey(cSoftware))
                using (var product = software.OpenSubKey(cProduct))
                using (var servers = product.OpenSubKey(cServers))
                {
                    foreach (string subKey in servers.GetSubKeyNames())
                    {
                        if (!int.TryParse(subKey, out int processId))
                        {
                            continue;
                        }

                        using (var server = servers.OpenSubKey(subKey))
                        {
                            if (server.GetValue(cPipeName) is string pipeName)
                            {
                                string processName = "(unknown)";
                                try
                                {
                                    using (var process = Process.GetProcessById(processId))
                                    {
                                        processName = process.ProcessName;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Trace.TraceError("Error getting process name: {0}", ex);
                                }

                                list.Add(new Server(processId, processName, pipeName));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error scanning for servers: {0}", ex);
            }

            return list.AsReadOnly();
        }
    }
}
