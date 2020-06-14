using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Connection
{
    [Flags]
    public enum LaunchOptions
    {
        Default = 0,
        MonitorAllFromStart = 1
    }

    public sealed class LaunchInfo
    {
        public string FileName { get; set; }
        public string Arguments { get; set; } = string.Empty;
        public LaunchOptions Options { get; set; } = LaunchOptions.Default;
    }
}
