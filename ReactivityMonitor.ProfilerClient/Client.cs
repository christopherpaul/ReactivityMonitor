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
                    OnModuleLoaded(msg.ModuleLoaded);
                    break;

                case MethodCallInstrumented:
                    OnMethodCallInstrumented(msg.MethodCallInstrumented);
                    break;

                case ObservableCreated:
                    OnObservableChain(msg.ObservableCreated);
                    break;

                case Subscribe:
                    OnSubscribe(msg.Subscribe);
                    break;

                case Unsubscribe:
                    OnUnsubscribe(msg.Unsubscribe);
                    break;

                case OnNext:
                    OnOnNext(msg.OnNext);
                    break;

                case OnCompleted:
                    OnOnCompleted(msg.OnCompleted);
                    break;

                case OnError:
                    OnOnError(msg.OnError);
                    break;
            }
        }

        private void OnOnError(Protocol.OnErrorEvent onError)
        {
            mModelUpdater.AddOnError(GetEventInfo(onError.Event), onError.SubscriptionId, onError.Message);
        }

        private void OnOnCompleted(Protocol.OnCompletedEvent onCompleted)
        {
            mModelUpdater.AddOnCompleted(GetEventInfo(onCompleted.Event), onCompleted.SubscriptionId);
        }

        private void OnOnNext(Protocol.OnNextEvent onNext)
        {
            mModelUpdater.AddOnNext(GetEventInfo(onNext.Event), onNext.SubscriptionId, onNext.ValueString);
        }

        private void OnUnsubscribe(Protocol.UnsubscribeEvent unsubscribe)
        {
            mModelUpdater.AddUnsubscription(GetEventInfo(unsubscribe.Event), unsubscribe.SubscriptionId);
        }

        private void OnSubscribe(Protocol.SubscribeEvent subscribe)
        {
            mModelUpdater.AddSubscription(GetEventInfo(subscribe.Event), subscribe.ObservableId);
        }

        private void OnModuleLoaded(Protocol.ModuleLoadedEvent e)
        {
            mModelUpdater.AddModule(e.ModuleID, e.Path);
        }

        private void OnMethodCallInstrumented(Protocol.MethodCallInstrumentedEvent e)
        {
            mModelUpdater.AddInstrumentedCall(
                e.InstrumentationPointId,
                e.ModuleId,
                e.OwningTypeName,
                e.CallingMethodName,
                e.CalledMethodName,
                e.InstructionOffset);
        }

        private void OnObservableChain(Protocol.ObservableCreatedEvent e)
        {
            mModelUpdater.AddObservableInstance(GetEventInfo(e.CreatedEvent), e.InstrumentationPointId);

            foreach (var inputObsId in e.InputObservableId)
            {
                mModelUpdater.RelateObservableInstances(inputObsId, e.CreatedEvent.SequenceId);
            }
        }

        private EventInfo GetEventInfo(Protocol.EventInfo info)
        {
            return new EventInfo(info.SequenceId, new DateTime(info.Timestamp, DateTimeKind.Utc), info.ThreadId);
        }

        private void SendRequest(Protocol.RequestMessage msg)
        {
            byte[] msgBytes = msg.ToByteArray();
            mChannel.SendMessage(msgBytes);
        }
    }
}
