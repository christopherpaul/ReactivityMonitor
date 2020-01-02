using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class ModuleLoadEvent
    {
        public ulong ModuleId { get; set; }
        public string ModulePath { get; set; }
        public string AssemblyName { get; set; }
    }
}
