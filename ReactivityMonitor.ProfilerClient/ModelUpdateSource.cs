using ReactivityMonitor.Model;
using ReactivityMonitor.Model.ModelUpdate;
using ReactivityProfiler.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace ReactivityMonitor.ProfilerClient
{
    internal sealed class ModelUpdateSource : IModelUpdateSource
    {
        private readonly IConnectableObservable<IGroupedObservable<EventMessage.EventOneofCase, EventMessage>> mMessageGroups;

        public ModelUpdateSource(IObservable<EventMessage> messages)
        {
            mMessageGroups = messages
                .GroupBy(msg => msg.EventCase)
                .Publish();

            Modules = GetMessages(EventMessage.EventOneofCase.ModuleLoaded, msg => msg.ModuleLoaded)
                .Select(msg => new NewModuleUpdate(msg.ModuleID, msg.Path));

            InstrumentedCalls = GetMessages(EventMessage.EventOneofCase.MethodCallInstrumented, msg => msg.MethodCallInstrumented)
                .Select(msg => new NewInstrumentedCall(msg.InstrumentationPointId, msg.ModuleId, msg.OwningTypeName, msg.CallingMethodName, msg.CalledMethodName, msg.InstructionOffset));

            ObservableInstances = GetMessages(EventMessage.EventOneofCase.ObservableCreated, msg => msg.ObservableCreated)
                .Select(msg => new NewObservableInstance(msg.CreatedEvent.ToModel(), msg.InstrumentationPointId));

            ObservableInstanceLinks = GetMessages(EventMessage.EventOneofCase.ObservablesLinked, msg => msg.ObservablesLinked)
                .Select(msg => new NewObservableInstanceLink(msg.InputObservableId, msg.OutputObservableId));

            CreatedSubscriptions = GetMessages(EventMessage.EventOneofCase.Subscribe, msg => msg.Subscribe)
                .Select(msg => new NewSubscription(msg.Event.ToModel(), msg.ObservableId));

            DisposedSubscriptions = GetMessages(EventMessage.EventOneofCase.Unsubscribe, msg => msg.Unsubscribe)
                .Select(msg => new DisposedSubscription(msg.Event.ToModel(), msg.SubscriptionId));

            var onNextEvents = GetMessages(EventMessage.EventOneofCase.OnNext, msg => msg.OnNext)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, new Model.OnNextEvent(msg.Event.ToModel(), msg.ValueString)));

            var onCompletedEvents = GetMessages(EventMessage.EventOneofCase.OnCompleted, msg => msg.OnCompleted)
                .Select(msg => new NewStreamEvent(msg.SubscriptionId, new Model.OnCompletedEvent(msg.Event.ToModel())));

            var onErrorEvents = GetMessages(EventMessage.EventOneofCase.OnError, msg => msg.OnError)
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
    }
}
