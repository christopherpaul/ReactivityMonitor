using DynamicData;
using ReactivityMonitor.Model.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class ReactivityModelSource
    {
        private const string cUnknown = "[unknown]";
        private static readonly Module cUnknownModule = new Module(ulong.MaxValue, cUnknown);

        private readonly ISourceCache<IModule, ulong> mModules;
        private readonly ISourceCache<IInstrumentedCall, int> mInstrumentedCalls;
        private readonly ISourceCache<IObservableInstance, long> mObservableInstances;
        private readonly ISourceCache<ISubscription, long> mSubscriptions;
        private readonly ConcurrentDictionary<long, ISubject<long>> mObservableInputs;
        private readonly ConcurrentDictionary<long, ISubject<long>> mObservableSubscriptions;
        private readonly ConcurrentDictionary<long, ISubject<StreamEvent>> mSubscriptionStreamEvents;

        private readonly ISourceCache<int, int> mRequestedInstrumentedCalls;

        public ReactivityModelSource()
        {
            mModules = new SourceCache<IModule, ulong>(m => m.ModuleId);
            mInstrumentedCalls = new SourceCache<IInstrumentedCall, int>(ic => ic.InstrumentedCallId);
            mObservableInstances = new SourceCache<IObservableInstance, long>(obs => obs.ObservableId);
            mSubscriptions = new SourceCache<ISubscription, long>(sub => sub.SubscriptionId);
            mObservableInputs = new ConcurrentDictionary<long, ISubject<long>>();
            mObservableSubscriptions = new ConcurrentDictionary<long, ISubject<long>>();
            mSubscriptionStreamEvents = new ConcurrentDictionary<long, ISubject<StreamEvent>>();

            mRequestedInstrumentedCalls = new SourceCache<int, int>(id => id);

            Model = new ModelImpl(this);
            Updater = new UpdaterImpl(this);
            ProfilerControl = new ProfilerControlImpl(this);
        }

        public IReactivityModel Model { get; }
        public IModelUpdater Updater { get; }
        public IProfilerControl ProfilerControl { get; }

        private ISubject<long> GetInputs(long obsId) =>
            mObservableInputs.GetOrAdd(obsId, _ => new ReplaySubject<long>());

        private ISubject<long> GetSubscriptions(long obsId) =>
            mObservableSubscriptions.GetOrAdd(obsId, _ => new ReplaySubject<long>());

        private ISubject<StreamEvent> GetStreamEvents(long subId) =>
            mSubscriptionStreamEvents.GetOrAdd(subId, _ => new ReplaySubject<StreamEvent>());

        private sealed class ModelImpl : IReactivityModel
        {
            private readonly ReactivityModelSource mParent;

            public ModelImpl(ReactivityModelSource parent)
            {
                Modules = parent.mModules.AsObservableCache();
                InstrumentedCalls = parent.mInstrumentedCalls.AsObservableCache();
                ObservableInstances = parent.mObservableInstances.AsObservableCache();
                mParent = parent;
            }

            public IObservableCache<IModule, ulong> Modules { get; }

            public IObservableCache<IInstrumentedCall, int> InstrumentedCalls { get; }

            public IObservableCache<IObservableInstance, long> ObservableInstances { get; }

            public void StartMonitorCall(int instrumentedCallId)
            {
                mParent.mRequestedInstrumentedCalls.AddOrUpdate(instrumentedCallId);
            }

            public void StopMonitorCall(int instrumentedCallId)
            {
                mParent.mRequestedInstrumentedCalls.RemoveKey(instrumentedCallId);
            }
        }

        private sealed class ProfilerControlImpl : IProfilerControl
        {
            public ProfilerControlImpl(ReactivityModelSource parent)
            {
                RequestedInstrumentedCallIds = parent.mRequestedInstrumentedCalls
                    .AsObservableCache()
                    .Connect()
                    .RemoveKey();
            }

            public IObservable<IChangeSet<int>> RequestedInstrumentedCallIds { get; }
        }

        private sealed class UpdaterImpl : IModelUpdater
        {
            private readonly ReactivityModelSource mParent;

            public UpdaterImpl(ReactivityModelSource parent)
            {
                mParent = parent;
            }

            public void AddInstrumentedCall(int id, ulong moduleId, string callingType, string callingMethod, string calledMethod, int instructionOffset)
            {
                var module = mParent.mModules.Lookup(moduleId);
                if (module.HasValue)
                {
                    var ic = new InstrumentedCall(id, module.Value, callingType, callingMethod, calledMethod, instructionOffset);
                    mParent.mInstrumentedCalls.AddOrUpdate(ic);
                    ((Module)module.Value).AddInstrumentedCall(ic);
                }
            }

            public void AddModule(ulong id, string path)
            {
                mParent.mModules.AddOrUpdate(new Module(id, path));
            }

            public void AddObservableInstance(EventInfo created, int instrumentationPoint)
            {
                var instrumentedCallMaybe = mParent.mInstrumentedCalls.Lookup(instrumentationPoint);
                var instrumentedCall = instrumentedCallMaybe.HasValue
                    ? instrumentedCallMaybe.Value
                    : AddPlaceholderCall();

                var inputs =
                    mParent.GetInputs(created.SequenceId)
                        .Distinct()
                        .SelectMany(inputId => mParent.mObservableInstances.WatchValue(inputId).Take(1));

                var subs =
                    mParent.GetSubscriptions(created.SequenceId)
                        .Distinct()
                        .SelectMany(subId => mParent.mSubscriptions.WatchValue(subId).Take(1));

                var obs = new ObservableInstance(created, instrumentedCall, inputs, subs);
                mParent.mObservableInstances.AddOrUpdate(obs);

                IInstrumentedCall AddPlaceholderCall()
                {
                    return new InstrumentedCall(instrumentationPoint, cUnknownModule, cUnknown, cUnknown, cUnknown, 0);
                }
            }

            public void RelateObservableInstances(long inputObsId, long outputObsId)
            {
                mParent.GetInputs(outputObsId).OnNext(inputObsId);
            }

            public void AddSubscription(EventInfo subscribed, long observableId)
            {
                var observableMaybe = mParent.mObservableInstances.Lookup(observableId);
                if (!observableMaybe.HasValue)
                {
                    Trace.TraceWarning("Couldn't find an observable with ID {0}", observableId);
                    return;
                }

                var events = mParent.GetStreamEvents(subscribed.SequenceId);
                events.OnNext(new SubscribeEvent(subscribed));

                var sub = new Subscription(subscribed.SequenceId, observableMaybe.Value, events);

                mParent.mSubscriptions.AddOrUpdate(sub);
                mParent.GetSubscriptions(observableId).OnNext(sub.SubscriptionId);
            }

            public void AddOnNext(EventInfo info, long subscriptionId)
            {
                mParent.GetStreamEvents(subscriptionId).OnNext(new OnNextEvent(info));
            }

            public void AddOnCompleted(EventInfo info, long subscriptionId)
            {
                mParent.GetStreamEvents(subscriptionId).OnNext(new OnCompletedEvent(info));
            }

            public void AddOnError(EventInfo info, long subscriptionId, string message)
            {
                mParent.GetStreamEvents(subscriptionId).OnNext(new OnErrorEvent(info, message));
            }

            public void AddUnsubscription(EventInfo info, long subscriptionId)
            {
                ISubject<StreamEvent> events = mParent.GetStreamEvents(subscriptionId);
                events.OnNext(new UnsubscribeEvent(info));
                events.OnCompleted();
            }
        }
    }
}
