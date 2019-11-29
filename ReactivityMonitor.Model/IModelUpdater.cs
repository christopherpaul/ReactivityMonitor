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

        void AddSubscription(EventInfo subscribed, long observableId);
        void AddOnNext(EventInfo info, long subscriptionId);
        void AddOnCompleted(EventInfo info, long subscriptionId);
        void AddOnError(EventInfo info, long subscriptionId, string message);
        void AddUnsubscription(EventInfo info, long subscriptionId);
    }
}
