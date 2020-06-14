using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.SettingsDto
{
    public sealed class LaunchMruEntry
    {
        public string ExecutablePath { get; set; }
        public string Arguments { get; set; }
        public bool MonitorAllFromStart { get; set; }
    }
}
