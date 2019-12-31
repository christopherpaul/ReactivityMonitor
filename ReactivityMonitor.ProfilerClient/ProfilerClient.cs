using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Protocol = ReactivityProfiler.Protocol;
using Google.Protobuf;

namespace ReactivityMonitor.ProfilerClient
{
    public static class ProfilerClient
    {
        private static readonly Protocol.RequestMessage cStartSendingInstrumentationEvents = new Protocol.RequestMessage
        {
            SendInstrumentationEvents = new Protocol.SendInstrumentationEventsRequest()
        };

        public static Model.IModelUpdateSource CreateModelUpdateSource(string pipeName, Model.IProfilerControl profilerControl)
        {
            var outgoingMessages = profilerControl.GetControlMessages()
                .StartWith(cStartSendingInstrumentationEvents)
                .Select(msg => msg.ToByteArray());

            var incomingMessages = ServerCommunication.CreateRawChannel(pipeName, outgoingMessages)
                .Select(Protocol.EventMessage.Parser.ParseFrom);

            var modelUpdateSource = new ModelUpdateSource(incomingMessages);

            return modelUpdateSource;
        }
    }
}
