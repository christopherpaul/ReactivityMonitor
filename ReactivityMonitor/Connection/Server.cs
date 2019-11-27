using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Connection
{
    public sealed class Server
    {
        public Server(int processId, string processName, string pipeName)
        {
            ProcessId = processId;
            ProcessName = processName;
            PipeName = pipeName;
        }

        public int ProcessId { get; }
        public string ProcessName { get; }
        public string PipeName { get; }
    }
}
