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
        private PayloadStore mPayloadStore;
        private ValueRenderer mValueRenderer;
        private TypeInfoStore mTypeInfoStore;
        private readonly ManualResetEventSlim mConnectedEvent;
        private int mFirstUnsentInstrumentationIndex;

        public Server(IStore store)
        {
            mStore = store;
            mConnectedEvent = new ManualResetEventSlim();
        }

        public void Start()
        {
            CreateChannel();
        }

        public void WaitUntilConnected()
        {
            Trace.TraceInformation("Waiting for client connection");
            mConnectedEvent.Wait();
        }

        private void CreateChannel()
        {
            var channel = new Channel(OnMessageReceived, OnChannelConnected, OnChannelDisconnected);
            mChannel = channel;
            mPayloadStore = new PayloadStore();
            mTypeInfoStore = new TypeInfoStore(typeToNotify => SendEvent(channel, new EventMessage { Type = typeToNotify }));
            mValueRenderer = new ValueRenderer(mPayloadStore, mTypeInfoStore);
            mChannel.Start();
            mStore.SinkEvents(new StoreEventSink(mChannel, mValueRenderer));
        }

        private void OnChannelConnected()
        {
            mConnectedEvent.Set();
        }

        private void OnChannelDisconnected()
        {
            mConnectedEvent.Reset();

            mStore.StopMonitoringAll();
            mChannel?.Dispose();
            mChannel = null;
            mValueRenderer = null;
            mPayloadStore = null;
            mFirstUnsentInstrumentationIndex = 0;

            CreateChannel();
        }

        private void OnMessageReceived(Stream message)
        {
            var requestMessage = RequestMessage.Parser.ParseFrom(message);

            switch (requestMessage.RequestCase)
            {
                case RequestMessage.RequestOneofCase.SendInstrumentationEvents:
                    switch (requestMessage.SendInstrumentationEvents.Mode)
                    {
                        case SendInstrumentationEventsRequest.Types.RequestMode.Continuous:
                            StartSendingInstrumentationEvents();
                            break;

                        case SendInstrumentationEventsRequest.Types.RequestMode.OnceAll:
                            SendAllInstrumentationEvents();
                            break;

                        case SendInstrumentationEventsRequest.Types.RequestMode.OnceUnsent:
                            SendAllUnsentInstrumentationEvents();
                            break;

                        default:
                            Trace.TraceWarning("Unknown Mode value in SendInstrumentationEventsRequest: {0}", (int)requestMessage.SendInstrumentationEvents.Mode);
                            break;
                    }
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

                case RequestMessage.RequestOneofCase.GetObjectProperties:
                    SendObjectProperties(requestMessage.GetObjectProperties.ObjectId);
                    break;

                case RequestMessage.RequestOneofCase.RecordEvent:
                    RecordClientEvent(requestMessage.RecordEvent);
                    break;

                case RequestMessage.RequestOneofCase.Disconnect:
                    Disconnect();
                    break;
            }
        }

        private void Disconnect()
        {
            var channel = mChannel;
            mChannel = null;

            channel.Dispose();
        }

        private void RecordClientEvent(RecordEventRequest req)
        {
            var details = mStore.RecordEvent();
            var eventMessage = new EventMessage
            {
                ClientEvent = new ClientEvent
                {
                    Event = GetEventInfo(details),
                    Id = req.Id,
                    Name = req.Name,
                    Description = req.Description
                }
            };

            SendEvent(mChannel, eventMessage);
        }

        private void SendObjectProperties(long requestedObjectId)
        {
            if (mPayloadStore.TryRetrieve(requestedObjectId, out object value) && value != null)
            {
                var type = value.GetType();
                IReadOnlyList<string> propertyNames = mTypeInfoStore.GetProperties(type);

                var objectProps = new Protocol.ObjectPropertiesResponse
                {
                    ObjectId = requestedObjectId
                };
                objectProps.PropertyValues.AddRange(
                    propertyNames
                        .Select(propName =>
                        {
                            try
                            {
                                object propValue = type.GetProperty(propName).GetValue(value);
                                return mValueRenderer.GetPayloadValue(propValue);
                            }
                            catch (Exception propEx)
                            {
                                if (propEx is System.Reflection.TargetInvocationException tiex)
                                {
                                    propEx = tiex.InnerException ?? propEx;
                                }
                                var renderedProp = mValueRenderer.GetPayloadValue(propEx);
                                renderedProp.IsExceptionGettingValue = true;
                                return renderedProp;
                            }
                        }));

                var responseMessage = new Protocol.EventMessage
                {
                    ObjectProperties = objectProps
                };

                SendEvent(mChannel, responseMessage);
            }
        }

        private void StartSendingInstrumentationEvents()
        {
            var channel = mChannel;
            var task = new Task(() => SendInstrumentationEvents(channel), TaskCreationOptions.LongRunning);
            task.Start();
        }

        private void SendAllInstrumentationEvents()
        {
            try
            {
                int index = 0;
                TrySendInstrumentationEventsFrom(ref index, mChannel);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Exception sending instrumentation events to client: {0}", ex);
            }
        }

        private void SendAllUnsentInstrumentationEvents()
        {
            try
            {
                int index = mFirstUnsentInstrumentationIndex;
                TrySendInstrumentationEventsFrom(ref index, mChannel);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Exception sending instrumentation events to client: {0}", ex);
            }
        }

        private void UpdateInstrumentationIndex(int index)
        {
            int oldIndex = mFirstUnsentInstrumentationIndex;
            while (index > oldIndex && Interlocked.CompareExchange(ref mFirstUnsentInstrumentationIndex, index, oldIndex) != oldIndex)
            {
                oldIndex = mFirstUnsentInstrumentationIndex;
            }
        }

        private void SendInstrumentationEvents(Channel channel)
        {
            try
            {
                int index = 0;
                while (channel.IsConnected)
                {
                    if (!TrySendInstrumentationEventsFrom(ref index, channel))
                    {
                        // We've sent all the existing events, so sleep a bit.
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Exception sending instrumentation events to client: {0}", ex);
            }
        }

        private bool TrySendInstrumentationEventsFrom(ref int index, Channel channel)
        {
            int eventCount = mStore.Instrumentation.GetEventCount();
            if (index >= eventCount)
            {
                // No events to send
                return false;
            }

            while (index < eventCount)
            {
                object e = mStore.Instrumentation.GetEvent(index);
                EventMessage msg = CreateInstrumentationMessage(e);
                SendEvent(channel, msg);
                index++;
            }

            UpdateInstrumentationIndex(index);

            return true;
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
                            Path = mle.ModulePath,
                            AssemblyName = mle.AssemblyName
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
                channel.SendMessage(msg.ToByteArray());
            }
        }

        private sealed class StoreEventSink : IStoreEventSink
        {
            private readonly Channel mChannel;
            private readonly ValueRenderer mValueRenderer;

            public StoreEventSink(Channel channel, ValueRenderer valueRenderer)
            {
                mChannel = channel;
                mValueRenderer = valueRenderer;
            }

            void IStoreEventSink.ObservableCreated(ObservableInfo obs)
            {
                var ev = new ObservableCreatedEvent()
                {
                    CreatedEvent = GetEventInfo(obs.Details),
                    InstrumentationPointId = obs.InstrumentationPoint
                };

                SendEvent(new EventMessage { ObservableCreated = ev });

                foreach (var input in obs.Inputs)
                {
                    SendObservablesLinkedEvent(obs, input);
                }
            }

            void IStoreEventSink.ObservablesLinked(ObservableInfo output, ObservableInfo input)
            {
                SendObservablesLinkedEvent(output, input);
            }

            private void SendObservablesLinkedEvent(ObservableInfo output, ObservableInfo input)
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
                        Value = mValueRenderer.GetPayloadValue(value)
                    }
                });
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
                        ExceptionValue = mValueRenderer.GetPayloadValue(error)
                    }
                });
            }

            private void SendEvent(EventMessage msg) => Server.SendEvent(mChannel, msg);
        }

        private static EventInfo GetEventInfo(CommonEventDetails details)
        {
            return new EventInfo
            {
                SequenceId = details.EventSequenceId,
                Timestamp = details.Timestamp.Ticks,
                ThreadId = details.ThreadId
            };
        }
    }
}
