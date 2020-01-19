using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Connection
{
    public interface IConnectionModel
    {
        Server Server { get; }
        IReactivityModel Model { get; }

        void StartMonitoringCall(int callId);
        void StopMonitoringCall(int callId);

        void PauseUpdates();
        void ResumeUpdates();

        void RequestObjectProperties(long objectId);
    }
}
