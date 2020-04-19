using DynamicData;
using ReactivityMonitor.Model.ModelUpdate;
using ReactivityMonitor.Utility.Extensions;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Text;
using Utility.CSharp;
using static Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedNameKind;

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
            private static readonly IModule cUnknownModule = new Module(0, string.Empty, string.Empty, Observable.Empty<IInstrumentedMethod>());
            private static readonly ISourceMethod cUnknownSourceMethod = new SourceMethod(cUnknownModule, string.Empty, string.Empty);
            private static readonly IInstrumentedMethod cUnknownMethod = new InstrumentedMethod(-1, cUnknownModule, 0, string.Empty, cUnknownSourceMethod);
            private static readonly IInstrumentedCall cUnknownInstrumentedCall = new InstrumentedCall(-1, cUnknownMethod, string.Empty, 0, Observable.Empty<IObservableInstance>());
            private static readonly IObservableInstance cUnknownObservable = new ObservableInstance(new EventInfo(-1, DateTime.MinValue, -1), Observable.Return(cUnknownInstrumentedCall), Observable.Empty<IObservableInstance>(), Observable.Empty<ISubscription>());

            private readonly IObservableCache<IModule, ulong> mModuleCache;
            private readonly IObservableCache<IInstrumentedMethod, int> mInstrumentedMethodCache;
            private readonly IObservableCache<IInstrumentedCall, int> mInstrumentedCallsCache;
            private readonly IObservableCache<IObservableInstance, long> mObservableInstancesCache;
            private readonly IObservableCache<ISubscription, long> mSubscriptionsCache;
            private readonly IObservableCache<UnsubscribeEvent, long> mSubscriptionDisposalsCache;
            private readonly IObservableCache<NewTypeInfo, int> mTypeInfoCache;
            private readonly IObservableCache<IObservable<IInstrumentedMethod>, ulong> mInstrumentedMethodsByModule;
            private readonly IObservableCache<IObservable<IObservableInstance>, int> mObservableInstancesByCall;
            private readonly IObservableCache<IObservable<IObservableInstance>, long> mObservableInstanceInputsByOutput;
            private readonly IObservableCache<IObservable<ISubscription>, long> mSubscriptionsByObservableInstance;
            private readonly IObservableCache<IObservable<StreamEvent>, long> mStreamEventsBySubscription;
            private readonly IObservableCache<ObjectPropertiesInfo, long> mObjectPropertiesInfoByObjectId;

            public Impl(IModelUpdateSource updateSource)
            {
                object moduleCacheLocker = new object();

                mModuleCache = updateSource.Modules
                    .ToObservableChangeSet(m => m.ModuleId)
                    .Transform(CreateModule)
                    .Synchronize(moduleCacheLocker)
                    .AsObservableCache();

                Modules = mModuleCache.Connect().SynchronizeSubscribe(moduleCacheLocker).Flatten().Select(chg => chg.Current);

                IObservable<IInstrumentedMethod> methods = updateSource.InstrumentedMethods
                    .Select(CreateInstrumentedMethod)
                    .Publish()
                    .ConnectForEver();

                object instrumentedMethodCacheLocker = new object();

                mInstrumentedMethodCache = methods
                    .ToObservableChangeSet(m => m.InstrumentedMethodId)
                    .Synchronize(instrumentedMethodCacheLocker)
                    .AsObservableCache();

                InstrumentedMethods = mInstrumentedMethodCache.Connect().SynchronizeSubscribe(instrumentedMethodCacheLocker)
                    .Flatten().Select(chg => chg.Current);

                object instrumentedCallsCacheLocker = new object();

                mInstrumentedCallsCache = methods.SelectMany(m => m.InstrumentedCalls)
                    .ToObservableChangeSet(c => c.InstrumentedCallId)
                    .Synchronize(instrumentedCallsCacheLocker)
                    .AsObservableCache();

                InstrumentedCalls = mInstrumentedCallsCache.Connect().SynchronizeSubscribe(instrumentedCallsCacheLocker)
                    .Flatten().Select(chg => chg.Current);

                object observableInstancesCacheLocker = new object();

                mObservableInstancesCache = updateSource.ObservableInstances
                    .ToObservableChangeSet(o => o.Created.SequenceId)
                    .Transform(CreateObservableInstance)
                    .Synchronize(observableInstancesCacheLocker)
                    .AsObservableCache();

                ObservableInstances = mObservableInstancesCache.Connect().SynchronizeSubscribe(observableInstancesCacheLocker)
                    .Flatten().Select(chg => chg.Current);

                mSubscriptionsCache = updateSource.CreatedSubscriptions
                    .ToObservableChangeSet(s => s.Subscribed.SequenceId)
                    .Transform(CreateSubscription)
                    .AsObservableCache();

                mSubscriptionDisposalsCache = updateSource.DisposedSubscriptions
                    .ToObservableChangeSet(d => d.SubscriptionId)
                    .Transform(d => new UnsubscribeEvent(d.Disposed))
                    .AsObservableCache();

                mInstrumentedMethodsByModule = methods
                    .GroupBy(m => m.Module.ModuleId)
                    .ToObservableChangeSet(grp => grp.Key)
                    .Transform(grp => grp.Replay().ConnectForEver())
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
                    .Transform(es => es.Select(CreateStreamEvent).Replay().ConnectForEver())
                    .AsObservableCache();

                mTypeInfoCache = updateSource.Types
                    .ToObservableChangeSet(t => t.TypeId)
                    .AsObservableCache();

                mObjectPropertiesInfoByObjectId = updateSource.ObjectPropertiesInfos
                    .ToObservableChangeSet(info => info.ObjectId)
                    .AsObservableCache();

                var clientEvents = updateSource.ClientEvents.Replay();
                clientEvents.Connect();
                ClientEvents = clientEvents.AsObservable();
            }

            public IObservable<IModule> Modules { get; }
            public IObservable<IInstrumentedMethod> InstrumentedMethods { get; }
            public IObservable<IInstrumentedCall> InstrumentedCalls { get; }
            public IObservable<IObservableInstance> ObservableInstances { get; }
            public IObservable<ClientEvent> ClientEvents { get; }

            private IModule CreateModule(NewModuleUpdate m)
            {
                var instrumentedMethods = mInstrumentedMethodsByModule
                    .WatchValue(m.ModuleId)
                    .Take(1)
                    .SelectMany(calls => calls);

                return new Module(m.ModuleId, m.Path, m.AssemblyName, instrumentedMethods);
            }

            private IInstrumentedMethod CreateInstrumentedMethod(NewInstrumentedMethod m)
            {
                var module = mModuleCache.Lookup(m.ModuleId);
                IModule moduleIfKnown = module.HasValue ? module.Value : cUnknownModule;
                var sourceMethod = GetSourceMethod(moduleIfKnown, m.OwningType, m.Name, out string localMethodName);
                var method = new InstrumentedMethod(m.Id, moduleIfKnown, m.MetadataToken, localMethodName, sourceMethod);
                method.InstrumentedCalls = m.InstrumentedCalls.Select(c => CreateInstrumentedCall(method, c)).ToImmutableList();
                return method;
            }

            private IInstrumentedCall CreateInstrumentedCall(IInstrumentedMethod method, NewInstrumentedCall c)
            {
                var observableInstances = mObservableInstancesByCall
                    .WatchValue(c.Id)
                    .Take(1)
                    .SelectMany(obses => obses);

                return new InstrumentedCall(c.Id, method, c.CalledMethod, c.InstructionOffset, observableInstances);
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

            private StreamEvent CreateStreamEvent(NewStreamEvent e)
            {
                switch (e.Kind)
                {
                    case StreamEvent.EventKind.OnCompleted:
                        return new OnCompletedEvent(e.Info);
                    case StreamEvent.EventKind.OnNext:
                        return new OnNextEvent(e.Info, TranslatePayload(e.Payload));
                    case StreamEvent.EventKind.OnError:
                        return new OnErrorEvent(e.Info, TranslatePayload(e.Payload));
                    default:
                        throw new ArgumentException(nameof(e));
                }

                object TranslatePayload(PayloadInfo payloadInfo)
                {
                    if (payloadInfo is SimplePayloadInfo s)
                    {
                        return s.Value;
                    }

                    var objPayload = (ObjectPayloadInfo)payloadInfo;
                    var typeInfo = mTypeInfoCache.Lookup(objPayload.TypeId).Value; // should always arrive before object that uses it

                    var properties = mObjectPropertiesInfoByObjectId.WatchValue(objPayload.ObjectId)
                        .Select(propsInfo => propsInfo.PropertyValues.Select(TranslatePayload))
                        .Select(propValues => typeInfo.PropertyNames.Zip(propValues, (name, val) => new KeyValuePair<string, object>(name, val)))
                        .Select(namesAndValues => namesAndValues.ToImmutableDictionary());

                    return new PayloadObject(
                        typeInfo.TypeName,
                        objPayload.ObjectId,
                        objPayload.Representation,
                        objPayload.IsExceptionGettingValue,
                        objPayload.ItemCount,
                        properties.Take(1),
                        Observable.Never<IImmutableList<object>>());
                }
            }

            private static readonly char[] cTypeNameSeparators = { '.', '+' };
            private ISourceMethod GetSourceMethod(IModule module, string parentType, string name, out string localName)
            {
                localName = name;
                string sourceParentType = parentType;

                int parentTypeLastSeparator = parentType.LastIndexOfAny(cTypeNameSeparators);
                if (parentTypeLastSeparator >= 0)
                {
                    string typeSimpleName = parentType.Substring(parentTypeLastSeparator + 1);

                    if (GeneratedNames.TryParseGeneratedName(typeSimpleName, out var genTypeNameKind, out _, out _))
                    {
                        sourceParentType = parentType.Remove(parentTypeLastSeparator);

                        // lambda display class names don't contain the method - need to parse the method name below

                        if (genTypeNameKind == StateMachineType)
                        {
                            if (GeneratedNames.TryParseSourceMethodNameFromGeneratedName(typeSimpleName, genTypeNameKind, out string sourceMethodName))
                            {
                                var sourceMethod = GetSourceMethod(module, sourceParentType, sourceMethodName, out localName);

                                localName += " [state machine]";

                                return sourceMethod;
                            }
                        }
                    }
                }

                if (GeneratedNames.TryParseGeneratedName(name, out var genMethodNameKind, out _, out _))
                {
                    if (GeneratedNames.TryParseSourceMethodNameFromGeneratedName(name, genMethodNameKind, out string sourceMethodName))
                    {
                        var sourceMethod = GetSourceMethod(module, sourceParentType, sourceMethodName, out localName);

                        switch (genMethodNameKind)
                        {
                            case IteratorFinallyMethod:
                                localName += " [iterator finally]";
                                break;

                            case LambdaMethod:
                                localName += " [lambda]";
                                break;

                            case LocalFunction:
                                if (GeneratedNames.TryParseLocalFunctionNameFromGeneratedName(name, out string localFunctionName))
                                {
                                    localName += "." + localFunctionName;
                                }
                                break;
                        }
                        return sourceMethod;
                    }
                }

                return new SourceMethod(module, parentType, name);
            }
        }
    }
}
