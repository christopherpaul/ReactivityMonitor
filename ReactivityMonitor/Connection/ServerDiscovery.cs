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

        private readonly HashSet<int> mBadProcessIds = new HashSet<int>();

        public bool TryGetServer(Process process, out Server server)
        {
            using (var software = Registry.CurrentUser.OpenSubKey(cSoftware))
            using (var product = software?.OpenSubKey(cProduct))
            using (var servers = product?.OpenSubKey(cServers))
            using (var key = servers?.OpenSubKey(process.Id.ToString()))
            {
                string pipeName = key?.GetValue(cPipeName) as string;
                if (pipeName == null)
                {
                    server = null;
                    return false;
                }

                server = new Server(process.Id, process.ProcessName, pipeName);
                return true;
            }
        }

        public IReadOnlyList<Server> Scan()
        {
            var list = new List<Server>();
            try
            {
                return ProfilerClient.ProcessDiscovery.GetAttachableProcesses()
                    .Select(p => new Server(p.Id, p.ProcessName, "dummy for now"))
                    .ToArray();
                //using (var software = Registry.CurrentUser.OpenSubKey(cSoftware))
                //using (var product = software?.OpenSubKey(cProduct))
                //using (var servers = product?.OpenSubKey(cServers))
                //{
                //    foreach (string subKey in servers?.GetSubKeyNames() ?? Enumerable.Empty<string>())
                //    {
                //        if (!int.TryParse(subKey, out int processId))
                //        {
                //            continue;
                //        }

                //        using (var server = servers.OpenSubKey(subKey))
                //        {
                //            if (server.GetValue(cPipeName) is string pipeName)
                //            {
                //                try
                //                {
                //                    using (var process = Process.GetProcessById(processId))
                //                    {
                //                        string processName = process.ProcessName;
                //                        list.Add(new Server(processId, processName, pipeName));
                //                    }
                //                }
                //                catch (Exception ex)
                //                {
                //                    if (mBadProcessIds.Add(processId))
                //                    {
                //                        Trace.TraceWarning("Could not get information about process with ID={0}: {1}", processId, ex);
                //                    }
                //                }
                //            }
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error scanning for servers: {0}", ex);
            }

            return list.AsReadOnly();
        }
    }
}
