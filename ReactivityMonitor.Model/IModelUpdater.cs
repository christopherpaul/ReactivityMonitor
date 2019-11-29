using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IModelUpdater
    {
        void AddModule(ulong id, string path);
        void AddInstrumentedCall(int id, ulong moduleId, string callingType, string callingMethod, string calledMethod, int instructionOffset);
        void AddObservableInstance(EventInfo created, int instrumentationPoint);
        void RelateObservableInstances(long inputObsId, long outputObsId);
    }
}
