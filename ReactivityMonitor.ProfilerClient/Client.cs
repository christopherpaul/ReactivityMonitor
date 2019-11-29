using Google.Protobuf;
using ReactivityMonitor.Model;
using Protocol = ReactivityProfiler.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using static ReactivityProfiler.Protocol.EventMessage.EventOneofCase;
using System.Reactive.Disposables;
using DynamicData;
using System.Linq;

namespace ReactivityMonitor.ProfilerClient
{
    public sealed class Client
    {
        private readonly string mPipeName;
        private readonly IModelUpdater mModelUpdater;
        private readonly IProfilerControl mProfilerControl;
        private Channel mChannel;
        private CompositeDisposable mDisposables;

        public Client(string pipeName, IModelUpdater modelUpdater, IProfilerControl profilerControl)
        {
            mPipeName = pipeName;
            mModelUpdater = modelUpdater;
            mProfilerControl = profilerControl;
        }

        public IDisposable Connect()
        {
            mDisposables = new CompositeDisposable();

            mChannel = new Channel(mPipeName, OnChannelConnected, OnMessageReceived);
            mDisposables.Add(mChannel);
            mChannel.Start();

            return mChannel;
        }

        private void OnChannelConnected()
        {
            // Kick off streaming of instrumentation events
            var request = new Protocol.RequestMessage
            {
                SendInstrumentationEvents = new Protocol.SendInstrumentationEventsRequest()
            };
            SendRequest(request);

            mDisposables.Add(
                mProfilerControl.RequestedInstrumentedCallIds
                    .OnItemAdded(StartMonitoringCall)
                    .OnItemRemoved(StopMonitoringCall)
                    .Subscribe());
        }

        private void StartMonitoringCall(int callId)
        {
            var request = new Protocol.StartMonitoringRequest();
            request.InstrumentationPointId.Add(callId);
            SendRequest(new Protocol.RequestMessage
            {
                StartMonitoring = request
            });
        }

        private void StopMonitoringCall(int callId)
        {
            var request = new Protocol.StopMonitoringRequest();
            request.InstrumentationPointId.Add(callId);
            SendRequest(new Protocol.RequestMessage
            {
                StopMonitoring = request
            });
        }

        private void OnMessageReceived(Stream msgStream)
        {
            var msg = Protocol.EventMessage.Parser.ParseFrom(msgStream);
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

        private void SendRequest(Protocol.RequestMessage msg)
        {
            byte[] msgBytes = msg.ToByteArray();
            mChannel.SendMessage(msgBytes);
        }
    }
}
