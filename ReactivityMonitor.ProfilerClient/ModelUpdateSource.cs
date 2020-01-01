using ReactivityMonitor.Model;
using ReactivityMonitor.Model.ModelUpdate;
using ReactivityMonitor.Utility.Extensions;
using ReactivityProfiler.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using static ReactivityProfiler.Protocol.EventMessage.EventOneofCase;

namespace ReactivityMonitor.ProfilerClient
{
    internal sealed class ModelUpdateSource : IModelUpdateSource
    {
        private readonly IConnectableObservable<IGroupedObservable<EventMessage.EventOneofCase, EventMessage>> mMessageGroups;
        private readonly Action<bool> mSetIsUpdating;

        public ModelUpdateSource(IObservable<EventMessage> messages)
        {
            var updating = new BehaviorSubject<bool>(true);
            mSetIsUpdating = updating.OnNext;

            mMessageGroups = messages
                .GateBySequenceNumber(updating, GetMessageSequenceNumber)
                .GroupBy(msg => msg.EventCase)
                .Publish();

            Modules = GetMessages(ModuleLoaded, msg => msg.ModuleLoaded)
                .Select(msg => new NewModuleUpdate(msg.ModuleID, msg.Path));

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
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, new Model.OnNextEvent(msg.Event.ToModel(), msg.ValueString)));

            var onCompletedEvents = GetMessages(OnCompleted, msg => msg.OnCompleted)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, new Model.OnCompletedEvent(msg.Event.ToModel())));

            var onErrorEvents = GetMessages(OnError, msg => msg.OnError)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, new Model.OnErrorEvent(msg.Event.ToModel(), msg.Message)));

            StreamEvents = new[] { onNextEvents, onCompletedEvents, onErrorEvents }.Merge();
        }

        public IObservable<NewModuleUpdate> Modules { get; }
        public IObservable<NewInstrumentedCall> InstrumentedCalls { get; }
        public IObservable<NewObservableInstance> ObservableInstances { get; }
        public IObservable<NewObservableInstanceLink> ObservableInstanceLinks { get; }
        public IObservable<NewSubscription> CreatedSubscriptions { get; }
        public IObservable<DisposedSubscription> DisposedSubscriptions { get; }
        public IObservable<NewStreamEvent> StreamEvents { get; }

        public IDisposable Connect()
        {
            return mMessageGroups.Connect();
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
