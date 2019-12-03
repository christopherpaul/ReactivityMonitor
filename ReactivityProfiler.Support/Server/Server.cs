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
using Google.Protobuf.Collections;
using System.Linq;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class Server
    {
        private readonly IStore mStore;
        private Channel mChannel;

        public Server(IStore store)
        {
            mStore = store;
        }

        public void Start()
        {
            CreateChannel();
        }

        private void CreateChannel()
        {
            mChannel = new Channel(OnMessageReceived, OnChannelDisconnected);
            mChannel.Start();
            mStore.SinkEvents(new StoreEventSink(mChannel));
        }

        private void OnChannelDisconnected()
        {
            mStore.StopMonitoringAll();
            mChannel.Dispose();
            mChannel = null;

            CreateChannel();
        }

        private void OnMessageReceived(Stream message)
        {
            var requestMessage = RequestMessage.Parser.ParseFrom(message);

            switch (requestMessage.RequestCase)
            {
                case RequestMessage.RequestOneofCase.SendInstrumentationEvents:
                    StartSendingInstrumentationEvents();
                    break;

                case RequestMessage.RequestOneofCase.StartMonitoring:
                    foreach (var id in requestMessage.StartMonitoring.InstrumentationPointId)
                    {
                        mStore.StartMonitoring(id);
                    }
                    break;

                case RequestMessage.RequestOneofCase.StopMonitoring:
                    foreach (var id in requestMessage.StopMonitoring.InstrumentationPointId)
                    {
                        mStore.StopMonitoring(id);
                    }
                    break;
            }
        }

        private void StartSendingInstrumentationEvents()
        {
            Trace.WriteLine("StartSendingInstrumentationEvents");
            var task = new Task(() => SendInstrumentationEvents(mChannel), TaskCreationOptions.LongRunning);
            task.Start();
        }

        private void SendInstrumentationEvents(Channel channel)
        {
            Trace.WriteLine("SendInstrumentationEvents");
            try
            {
                int index = 0;
                while (channel.IsConnected)
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
                        EventMessage msg = CreateInstrumentationMessage(e);
                        SendEvent(channel, msg);
                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Exception sending instrumentation events to client: {0}", ex);
            }
        }

        private EventMessage CreateInstrumentationMessage(object e)
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
                            CalledMethodName = mcie.CalledMethodName,
                            OwningTypeName = mcie.OwningTypeName,
                            CallingMethodName = mcie.CallingMethodName
                        }
                    };

                default:
                    throw new ArgumentException("Unrecognised event object");
            }
        }

        private static void SendEvent(Channel channel, EventMessage msg)
        {
            if (channel != null)
            {
                Trace.WriteLine($"Sending message to client: {msg}");
                channel.SendMessage(msg.ToByteArray());
            }
        }

        private sealed class StoreEventSink : IStoreEventSink
        {
            private readonly Channel mChannel;

            public StoreEventSink(Channel channel)
            {
                mChannel = channel;
            }

            void IStoreEventSink.ObservableCreated(ObservableInfo obs)
            {
                var ev = new ObservableCreatedEvent()
                {
                    CreatedEvent = GetEventInfo(obs.Details),
                    InstrumentationPointId = obs.InstrumentationPoint
                };
                ev.InputObservableId.Add(obs.Inputs.Select(i => i.ObservableId));

                SendEvent(new EventMessage { ObservableCreated = ev });
            }

            void IStoreEventSink.ObservablesLinked(ObservableInfo output, ObservableInfo input)
            {
                var ev = new ObservablesLinkedEvent
                {
                    OutputObservableId = output.ObservableId,
                    InputObservableId = input.ObservableId
                };

                SendEvent(new EventMessage { ObservablesLinked = ev });
            }

            void IStoreEventSink.Subscribed(SubscriptionInfo sub)
            {
                SendEvent(new EventMessage
                {
                    Subscribe = new SubscribeEvent
                    {
                        Event = GetEventInfo(sub.Details),
                        ObservableId = sub.Observable.ObservableId
                    }
                });
            }

            void IStoreEventSink.Unsubscribed(ref CommonEventDetails details, SubscriptionInfo sub)
            {
                SendEvent(new EventMessage
                {
                    Unsubscribe = new UnsubscribeEvent
                    {
                        Event = GetEventInfo(details),
                        SubscriptionId = sub.SubscriptionId
                    }
                });
            }

            void IStoreEventSink.OnNext<T>(ref CommonEventDetails details, SubscriptionInfo sub, T value)
            {
                SendEvent(new EventMessage
                {
                    OnNext = new OnNextEvent
                    {
                        Event = GetEventInfo(details),
                        SubscriptionId = sub.SubscriptionId,
                        ValueString = GetStringFor(value)
                    }
                });
            }

            private string GetStringFor<T>(T value)
            {
                try
                {
                    return value.ToString();
                }
                catch
                {
                    return string.Empty;
                }
            }

            void IStoreEventSink.OnCompleted(ref CommonEventDetails details, SubscriptionInfo sub)
            {
                SendEvent(new EventMessage
                {
                    OnCompleted = new OnCompletedEvent
                    {
                        Event = GetEventInfo(details),
                        SubscriptionId = sub.SubscriptionId
                    }
                });
            }

            void IStoreEventSink.OnError(ref CommonEventDetails details, SubscriptionInfo sub, Exception error)
            {
                SendEvent(new EventMessage
                {
                    OnError = new OnErrorEvent
                    {
                        Event = GetEventInfo(details),
                        SubscriptionId = sub.SubscriptionId,
                        Message = error.Message
                    }
                });
            }

            private EventInfo GetEventInfo(CommonEventDetails details)
            {
                return new EventInfo
                {
                    SequenceId = details.EventSequenceId,
                    Timestamp = details.Timestamp.Ticks,
                    ThreadId = details.ThreadId
                };
            }

            private void SendEvent(EventMessage msg) => Server.SendEvent(mChannel, msg);
        }
    }
}
