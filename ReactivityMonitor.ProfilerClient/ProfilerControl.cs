using Model = ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Text;
using DynamicData;
using System.Reactive.Linq;
using Protocol = ReactivityProfiler.Protocol;

namespace ReactivityMonitor.ProfilerClient
{
    internal static class ProfilerControl
    {
        public static IObservable<Protocol.RequestMessage> GetControlMessages(this Model.IProfilerControl profilerControl)
        {
            var callMonitoringRequests = profilerControl.RequestedInstrumentedCallIds
                .Publish(changes =>
                {
                    var startMonitoringMessages = changes
                        .WhereReasonsAre(ChangeReason.Add)
                        .Flatten()
                        .Select(add =>
                        {
                            var req = new Protocol.StartMonitoringRequest();
                            req.InstrumentationPointId.Add(add.Key);
                            return new Protocol.RequestMessage
                            {
                                StartMonitoring = req
                            };
                        });

                    var stopMonitoringMessages = changes
                        .WhereReasonsAre(ChangeReason.Remove)
                        .Flatten()
                        .Select(remove =>
                        {
                            var req = new Protocol.StopMonitoringRequest();
                            req.InstrumentationPointId.Add(remove.Key);
                            return new Protocol.RequestMessage
                            {
                                StopMonitoring = req
                            };
                        });

                    return new[] { startMonitoringMessages, stopMonitoringMessages }.Merge();
                });

            var objectDataRequests = profilerControl.ObjectDataRequests
                .Select(req => new Protocol.RequestMessage
                {
                    GetObjectProperties = new Protocol.ObjectPropertiesRequest
                    {
                        ObjectId = req.ObjectId
                    }
                });

            return new[] { callMonitoringRequests, objectDataRequests }.Merge();
        }
    }
}
