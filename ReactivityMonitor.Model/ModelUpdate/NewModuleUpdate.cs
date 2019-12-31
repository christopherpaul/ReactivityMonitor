using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewModuleUpdate
    {
        public NewModuleUpdate(ulong moduleId, string path)
        {
            ModuleId = moduleId;
            Path = path;
        }

        public ulong ModuleId { get; }
        public string Path { get; }
    }
}
