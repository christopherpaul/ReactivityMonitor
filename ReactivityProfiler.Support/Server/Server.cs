using ReactivityProfiler.Protocol;
using ReactivityProfiler.Support.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using System.Threading;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class Server
    {
        private readonly IStore mStore;
        private readonly Channel mChannel;

        public Server(IStore store)
        {
            mStore = store;
            mChannel = new Channel(OnMessageReceived);
        }

        public void Start()
        {
            mChannel.Start();
        }

        private void OnMessageReceived(Stream message)
        {
            var requestMessage = RequestMessage.Parser.ParseFrom(message);

            switch (requestMessage.RequestCase)
            {
                case RequestMessage.RequestOneofCase.SendInstrumentationEvents:
                    StartSendingInstrumentationEvents();
                    break;
            }
        }

        private void StartSendingInstrumentationEvents()
        {
            Trace.WriteLine("StartSendingInstrumentationEvents");
            var task = new Task(SendInstrumentationEvents, TaskCreationOptions.LongRunning);
            task.Start();
        }

        private void SendInstrumentationEvents()
        {
            Trace.WriteLine("SendInstrumentationEvents");
            try
            {
                int index = 0;
                while (mChannel.IsConnected)
                {
                    int eventCount = mStore.Instrumentation.GetEventCount();
                    if (index == eventCount)
                    {
                        // We've sent all the existing events, so sleep a bit.
                        Thread.Sleep(1000);
                    }

                    while (index < eventCount)
                    {
                        object e = mStore.Instrumentation.GetEvent(index);
                        EventMessage msg = CreateEventMessage(e);
                        Trace.WriteLine($"SendInstrumentationEvents: sending {msg}");
                        mChannel.SendMessage(msg.ToByteArray());
                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Exception sending instrumentation events to client: {0}", ex);
            }
        }

        private EventMessage CreateEventMessage(object e)
        {
            switch (e)
            {
                case ModuleLoadEvent mle:
                    return new EventMessage
                    {
                        ModuleLoaded = new ModuleLoadedEvent
                        {
                            ModuleID = mle.ModuleId,
                            Path = mle.ModulePath
                        }
                    };

                case Store.MethodCallInstrumentedEvent mcie:
                    return new EventMessage
                    {
                        MethodCallInstrumented = new Protocol.MethodCallInstrumentedEvent
                        {
                            InstrumentationPointId = mcie.InstrumentationPointId,
                            ModuleId = mcie.ModuleId,
                            FunctionToken = mcie.FunctionToken,
                            InstructionOffset = mcie.InstructionOffset,
                            CalledMethodName = mcie.CalledMethodName
                        }
                    };

                default:
                    throw new ArgumentException("Unrecognised event object");
            }
        }
    }
}
