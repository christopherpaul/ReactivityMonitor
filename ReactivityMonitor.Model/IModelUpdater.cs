using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IModelUpdater
    {
        void AddModule(ulong id, string path);
        void AddInstrumentedCall(int id, ulong moduleId);
    }
}
