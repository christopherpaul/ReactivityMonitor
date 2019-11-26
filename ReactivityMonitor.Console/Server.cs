using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.ConsoleApp
{
    internal sealed class Server
    {
        public Server(string processName, string pipeName)
        {
            ProcessName = processName;
            PipeName = pipeName;
        }

        public string ProcessName { get; }
        public string PipeName { get; }
    }
}
