using ReactivityMonitor.Model;
using ReactivityMonitor.Model.ModelUpdate;
using ReactivityMonitor.Utility.Extensions;
using ReactivityProfiler.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using static ReactivityProfiler.Protocol.EventMessage.EventOneofCase;
using VC = ReactivityProfiler.Protocol.Value.ValueOneofCase;

namespace ReactivityMonitor.ProfilerClient
{
    internal sealed class ModelUpdateSource : IModelUpdateSource
    {
        private readonly IConnectableObservable<EventMessage> mMessages;
        private readonly IConnectableObservable<IGroupedObservable<EventMessage.EventOneofCase, EventMessage>> mMessageGroups;
        private readonly Action<bool> mSetIsUpdating;

        public ModelUpdateSource(IObservable<EventMessage> messages)
        {
            var updating = new BehaviorSubject<bool>(true);
            mSetIsUpdating = updating.OnNext;

            mMessages = messages.Publish();

            mMessageGroups = mMessages
                .GateBySequenceNumber(updating, GetMessageSequenceNumber)
                .GroupBy(msg => msg.EventCase)
                .Publish();

            Modules = GetMessages(ModuleLoaded, msg => msg.ModuleLoaded)
                .Select(msg => new NewModuleUpdate(msg.ModuleID, msg.Path, msg.AssemblyName));

            InstrumentedCalls = GetMessages(MethodCallInstrumented, msg => msg.MethodCallInstrumented)
                .Select(msg => new NewInstrumentedCall(msg.InstrumentationPointId, msg.ModuleId, msg.OwningTypeName, msg.CallingMethodName, msg.CalledMethodName, msg.InstructionOffset));

            ObservableInstances = GetMessages(ObservableCreated, msg => msg.ObservableCreated)
                .Select(msg => new NewObservableInstance(msg.CreatedEvent.ToModel(), msg.InstrumentationPointId));

            ObservableInstanceLinks = GetMessages(ObservablesLinked, msg => msg.ObservablesLinked)
                .Select(msg => new NewObservableInstanceLink(msg.InputObservableId, msg.OutputObservableId));

            CreatedSubscriptions = GetMessages(Subscribe, msg => msg.Subscribe)
                .Select(msg => new NewSubscription(msg.Event.ToModel(), msg.ObservableId));

            DisposedSubscriptions = GetMessages(Unsubscribe, msg => msg.Unsubscribe)
                .Select(msg => new DisposedSubscription(msg.Event.ToModel(), msg.SubscriptionId));

            var onNextEvents = GetMessages(OnNext, msg => msg.OnNext)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, StreamEvent.EventKind.OnNext, msg.Event.ToModel(), GetPayloadInfo(msg.Value)));

            var onCompletedEvents = GetMessages(OnCompleted, msg => msg.OnCompleted)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, StreamEvent.EventKind.OnCompleted, msg.Event.ToModel(), null));

            var onErrorEvents = GetMessages(OnError, msg => msg.OnError)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, StreamEvent.EventKind.OnError, msg.Event.ToModel(), GetPayloadInfo(msg.ExceptionValue)));

            StreamEvents = new[] { onNextEvents, onCompletedEvents, onErrorEvents }.Merge();

            Types = GetMessages(EventMessage.EventOneofCase.Type, msg => msg.Type)
                .Select(msg => new NewTypeInfo(msg.TypeId, msg.TypeName, msg.PropertyNames));

            ObjectPropertiesInfos = GetMessages(ObjectProperties, msg => msg.ObjectProperties)
                .Select(CreateObjectPropertiesInfo);

            ClientEvents = GetMessages(EventMessage.EventOneofCase.ClientEvent, msg => msg.ClientEvent)
                .Select(msg => new Model.ClientEvent(msg.Event.ToModel(), msg.Description));
        }

        public IObservable<NewModuleUpdate> Modules { get; }
        public IObservable<NewInstrumentedCall> InstrumentedCalls { get; }
        public IObservable<NewObservableInstance> ObservableInstances { get; }
        public IObservable<NewObservableInstanceLink> ObservableInstanceLinks { get; }
        public IObservable<NewSubscription> CreatedSubscriptions { get; }
        public IObservable<DisposedSubscription> DisposedSubscriptions { get; }
        public IObservable<NewStreamEvent> StreamEvents { get; }
        public IObservable<NewTypeInfo> Types { get; }
        public IObservable<ObjectPropertiesInfo> ObjectPropertiesInfos { get; }
        public IObservable<Model.ClientEvent> ClientEvents { get; }

        public IDisposable Connect()
        {
            var groupsConnection = mMessageGroups.Connect();
            var messagesConnection = mMessages.Connect();
            return StableCompositeDisposable.Create(groupsConnection, messagesConnection);
        }

        private IObservable<T> GetMessages<T>(EventMessage.EventOneofCase messageKind, Func<EventMessage, T> messageSelector)
        {
            return mMessageGroups
                .Where(g => g.Key == messageKind)
                .Take(1)
                .SelectMany(g => g)
                .Select(messageSelector);
        }

        private long GetMessageSequenceNumber(EventMessage msg)
        {
            switch (msg.EventCase)
            {
                case ModuleLoaded: return 0;
                case MethodCallInstrumented: return 0;
                case ObservableCreated: return msg.ObservableCreated.CreatedEvent.SequenceId;
                case ObservablesLinked: return 0;
                case Subscribe: return msg.Subscribe.Event.SequenceId;
                case Unsubscribe: return msg.Unsubscribe.Event.SequenceId;
                case OnNext: return msg.OnNext.Event.SequenceId;
                case OnCompleted: return msg.OnCompleted.Event.SequenceId;
                case OnError: return msg.OnError.Event.SequenceId;
                default: return 0;
            }
        }

        private PayloadInfo GetPayloadInfo(Value value)
        {
            int typeId = value.TypeId;
            switch (value.ValueCase)
            {
                case VC.Bool:
                    return new SimplePayloadInfo(typeId, value.Bool);
                case VC.ByteString:
                    return new SimplePayloadInfo(typeId, value.ByteString.ToByteArray());
                case VC.Char:
                    return new SimplePayloadInfo(typeId, (char)value.Char);
                case VC.DateTimeLocal:
                    return new SimplePayloadInfo(typeId, new DateTime(value.DateTimeLocal, DateTimeKind.Local));
                case VC.DateTimeUtc:
                    return new SimplePayloadInfo(typeId, new DateTime(value.DateTimeUtc, DateTimeKind.Utc));
                case VC.DateTimeUnspecified:
                    return new SimplePayloadInfo(typeId, new DateTime(value.DateTimeUnspecified, DateTimeKind.Unspecified));
                case VC.Double:
                    return new SimplePayloadInfo(typeId, value.Double);
                case VC.Int64:
                    return new SimplePayloadInfo(typeId, value.Int64);
                case VC.Null:
                case VC.None:
                default:
                    return new SimplePayloadInfo(typeId, null);
                case VC.String:
                    return new SimplePayloadInfo(typeId, value.String);
                case VC.Timespan:
                    return new SimplePayloadInfo(typeId, new TimeSpan(value.Timespan));
                case VC.UInt64:
                    return new SimplePayloadInfo(typeId, value.UInt64);
                case VC.Object:
                    return new ObjectPayloadInfo(typeId, value.Object.ObjectId, value.Object.StringRepresentation, value.IsExceptionGettingValue, value.Object.HasItemCount ? (int?)value.Object.ItemCount : null);
            }
        }

        private ObjectPropertiesInfo CreateObjectPropertiesInfo(ObjectPropertiesResponse msg)
        {
            return new ObjectPropertiesInfo(msg.ObjectId, msg.PropertyValues.Select(GetPayloadInfo).ToImmutableList());
        }


        public void Pause()
        {
            mSetIsUpdating(false);
        }

        public void Resume()
        {
            mSetIsUpdating(true);
        }
    }
}
