using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewModuleUpdate
    {
        public NewModuleUpdate(ulong moduleId, string path, string assemblyName)
        {
            ModuleId = moduleId;
            Path = path;
            AssemblyName = assemblyName;
        }

        public ulong ModuleId { get; }
        public string Path { get; }
        public string AssemblyName { get; }
    }
}
