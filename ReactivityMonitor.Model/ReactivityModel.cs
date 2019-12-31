using DynamicData;
using ReactivityMonitor.Model.ModelUpdate;
using ReactivityMonitor.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace ReactivityMonitor.Model
{
    public static class ReactivityModel
    {
        public static IReactivityModel Create(IModelUpdateSource updateSource)
        {
            return new Impl(updateSource);
        }

        private sealed class Impl : IReactivityModel
        {
            private static readonly IModule cUnknownModule = new Module(0, string.Empty, Observable.Empty<IInstrumentedCall>());
            private static readonly IInstrumentedCall cUnknownInstrumentedCall = new InstrumentedCall(-1, Observable.Return(cUnknownModule), string.Empty, string.Empty, string.Empty, 0, Observable.Empty<IObservableInstance>());
            private static readonly IObservableInstance cUnknownObservable = new ObservableInstance(new EventInfo(-1, DateTime.MinValue, -1), Observable.Return(cUnknownInstrumentedCall), Observable.Empty<IObservableInstance>(), Observable.Empty<ISubscription>());

            private readonly IObservableCache<IModule, ulong> mModuleCache;
            private readonly IObservableCache<IInstrumentedCall, int> mInstrumentedCallsCache;
            private readonly IObservableCache<IObservableInstance, long> mObservableInstancesCache;
            private readonly IObservableCache<ISubscription, long> mSubscriptionsCache;
            private readonly IObservableCache<UnsubscribeEvent, long> mSubscriptionDisposalsCache;
            private readonly IObservableCache<IObservable<IInstrumentedCall>, ulong> mInstrumentedCallsByModule;
            private readonly IObservableCache<IObservable<IObservableInstance>, int> mObservableInstancesByCall;
            private readonly IObservableCache<IObservable<IObservableInstance>, long> mObservableInstanceInputsByOutput;
            private readonly IObservableCache<IObservable<ISubscription>, long> mSubscriptionsByObservableInstance;
            private readonly IObservableCache<IObservable<StreamEvent>, long> mStreamEventsBySubscription;

            public Impl(IModelUpdateSource updateSource)
            {
                mModuleCache = updateSource.Modules
                    .ToObservableChangeSet(m => m.ModuleId)
                    .Transform(CreateModule)
                    .AsObservableCache();

                mInstrumentedCallsCache = updateSource.InstrumentedCalls
                    .ToObservableChangeSet(c => c.Id)
                    .Transform(CreateInstrumentedCall)
                    .AsObservableCache();

                mObservableInstancesCache = updateSource.ObservableInstances
                    .ToObservableChangeSet(o => o.Created.SequenceId)
                    .Transform(CreateObservableInstance)
                    .AsObservableCache();

                mSubscriptionsCache = updateSource.CreatedSubscriptions
                    .ToObservableChangeSet(s => s.Subscribed.SequenceId)
                    .Transform(CreateSubscription)
                    .AsObservableCache();

                mSubscriptionDisposalsCache = updateSource.DisposedSubscriptions
                    .ToObservableChangeSet(d => d.SubscriptionId)
                    .Transform(d => new UnsubscribeEvent(d.Disposed))
                    .AsObservableCache();

                mInstrumentedCallsByModule = updateSource.InstrumentedCalls
                    .GroupBy(c => c.ModuleId)
                    .ToObservableChangeSet(grp => grp.Key)
                    .Transform(calls => calls.SelectMany(c => mInstrumentedCallsCache.WatchValue(c.Id).Take(1)).Replay().ConnectForEver())
                    .AsObservableCache();

                mObservableInstancesByCall = updateSource.ObservableInstances
                    .GroupBy(o => o.InstrumentedCallId)
                    .ToObservableChangeSet(grp => grp.Key)
                    .Transform(obses => obses.SelectMany(obs => mObservableInstancesCache.WatchValue(obs.Created.SequenceId).Take(1)).Replay().ConnectForEver())
                    .AsObservableCache();

                mObservableInstanceInputsByOutput = updateSource.ObservableInstanceLinks
                    .GroupBy(l => l.OutputId)
                    .ToObservableChangeSet(grp => grp.Key)
                    .Transform(links => links.SelectMany(link => mObservableInstancesCache.WatchValue(link.InputId).Take(1)).Replay().ConnectForEver())
                    .AsObservableCache();

                mSubscriptionsByObservableInstance = updateSource.CreatedSubscriptions
                    .GroupBy(s => s.ObservableId)
                    .ToObservableChangeSet(grp => grp.Key)
                    .Transform(subs => subs.SelectMany(sub => mSubscriptionsCache.WatchValue(sub.Subscribed.SequenceId).Take(1)).Replay().ConnectForEver())
                    .AsObservableCache();

                mStreamEventsBySubscription = updateSource.StreamEvents
                    .GroupBy(e => e.SubscriptionId)
                    .ToObservableChangeSet(grp => grp.Key)
                    .Transform(es => es.Select(e => e.StreamEvent).Replay().ConnectForEver())
                    .AsObservableCache();
            }

            public IObservable<IModule> Modules => mModuleCache.Connect().Flatten().Select(chg => chg.Current);
            public IObservable<IInstrumentedCall> InstrumentedCalls => mInstrumentedCallsCache.Connect().Flatten().Select(chg => chg.Current);
            public IObservable<IObservableInstance> ObservableInstances => mObservableInstancesCache.Connect().Flatten().Select(chg => chg.Current);

            private IModule CreateModule(NewModuleUpdate m)
            {
                var instrumentedCalls = mInstrumentedCallsByModule
                    .WatchValue(m.ModuleId)
                    .Take(1)
                    .SelectMany(calls => calls);

                return new Module(m.ModuleId, m.Path, instrumentedCalls);
            }

            private IInstrumentedCall CreateInstrumentedCall(NewInstrumentedCall c)
            {
                var observableInstances = mObservableInstancesByCall
                    .WatchValue(c.Id)
                    .Take(1)
                    .SelectMany(obses => obses);

                IObservable<IModule> module = mModuleCache
                    .WatchValue(c.ModuleId)
                    .Take(1)
                    .StartWith(cUnknownModule);

                return new InstrumentedCall(c.Id, module, c.CallingType, c.CallingMethod, c.CalledMethod, c.InstructionOffset, observableInstances);
            }

            private IObservableInstance CreateObservableInstance(NewObservableInstance o)
            {
                var inputs = mObservableInstanceInputsByOutput
                    .WatchValue(o.Created.SequenceId)
                    .Take(1)
                    .SelectMany(inputs => inputs);

                var subscriptions = mSubscriptionsByObservableInstance
                    .WatchValue(o.Created.SequenceId)
                    .Take(1)
                    .SelectMany(subs => subs);

                var call = mInstrumentedCallsCache
                    .WatchValue(o.InstrumentedCallId)
                    .Take(1)
                    .StartWith(cUnknownInstrumentedCall);

                return new ObservableInstance(o.Created, call, inputs, subscriptions);
            }

            private ISubscription CreateSubscription(NewSubscription s)
            {
                var obs = mObservableInstancesCache
                    .WatchValue(s.ObservableId)
                    .Take(1)
                    .StartWith(cUnknownObservable);

                var streamEvents = mStreamEventsBySubscription
                    .WatchValue(s.Subscribed.SequenceId)
                    .Take(1)
                    .SelectMany(es => es);

                var unsubscribeEvent = mSubscriptionDisposalsCache
                    .WatchValue(s.Subscribed.SequenceId)
                    .Take(1);

                var allEvents = streamEvents
                    .Merge(unsubscribeEvent)
                    .StartWith(new SubscribeEvent(s.Subscribed));

                return new Subscription(s.Subscribed.SequenceId, obs, allEvents);
            }
        }
    }
}
