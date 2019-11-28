using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Connection
{
    public sealed class LaunchInfo
    {
        public string FileName { get; set; }
        public string Arguments { get; set; } = string.Empty;
    }
}
