using Google.Protobuf;
using ReactivityMonitor.Model;
using ReactivityProfiler.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using static ReactivityProfiler.Protocol.EventMessage.EventOneofCase;

namespace ReactivityMonitor.ProfilerClient
{
    public sealed class Client
    {
        private readonly string mPipeName;
        private readonly IModelUpdater mModelUpdater;
        private Channel mChannel;

        public Client(string pipeName, IModelUpdater modelUpdater, IProfilerControl profilerControl)
        {
            mPipeName = pipeName;
            mModelUpdater = modelUpdater;
        }

        public IDisposable Connect()
        {
            mChannel = new Channel(mPipeName, OnChannelConnected, OnMessageReceived);
            mChannel.Start();

            return mChannel;
        }

        private void OnChannelConnected()
        {
            // Kick off streaming of instrumentation events
            var request = new RequestMessage
            {
                SendInstrumentationEvents = new SendInstrumentationEventsRequest()
            };
            SendRequest(request);
        }

        private void OnMessageReceived(Stream msgStream)
        {
            var msg = EventMessage.Parser.ParseFrom(msgStream);
            Trace.WriteLine($"Received message from server: {msg}");
            switch (msg.EventCase)
            {
                case ModuleLoaded:
                    mModelUpdater.AddModule(
                        msg.ModuleLoaded.ModuleID,
                        msg.ModuleLoaded.Path);
                    break;

                case MethodCallInstrumented:
                    mModelUpdater.AddInstrumentedCall(
                        msg.MethodCallInstrumented.InstrumentationPointId,
                        msg.MethodCallInstrumented.ModuleId,
                        msg.MethodCallInstrumented.OwningTypeName,
                        msg.MethodCallInstrumented.CallingMethodName,
                        msg.MethodCallInstrumented.CalledMethodName,
                        msg.MethodCallInstrumented.InstructionOffset);
                    break;
            }
        }

        private void SendRequest(RequestMessage msg)
        {
            byte[] msgBytes = msg.ToByteArray();
            mChannel.SendMessage(msgBytes);
        }
    }
}
