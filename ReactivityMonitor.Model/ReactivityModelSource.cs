using DynamicData;
using ReactivityMonitor.Model.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
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
        private readonly ConcurrentDictionary<long, ISourceCache<long, long>> mObservableInputs;

        private readonly ISourceCache<int, int> mRequestedInstrumentedCalls;

        public ReactivityModelSource()
        {
            mModules = new SourceCache<IModule, ulong>(m => m.ModuleId);
            mInstrumentedCalls = new SourceCache<IInstrumentedCall, int>(ic => ic.InstrumentedCallId);
            mObservableInstances = new SourceCache<IObservableInstance, long>(obs => obs.ObservableId);
            mObservableInputs = new ConcurrentDictionary<long, ISourceCache<long, long>>();

            mRequestedInstrumentedCalls = new SourceCache<int, int>(id => id);

            Model = new ModelImpl(this);
            Updater = new UpdaterImpl(this);
            ProfilerControl = new ProfilerControlImpl(this);
        }

        public IReactivityModel Model { get; }
        public IModelUpdater Updater { get; }
        public IProfilerControl ProfilerControl { get; }

        private ISourceCache<long, long> GetInputsCache(long obsId) =>
            mObservableInputs.GetOrAdd(obsId, _ => new SourceCache<long, long>(id => id));

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
                    mParent.GetInputsCache(created.SequenceId).Connect()
                        .MergeMany(inputId => mParent.mObservableInstances.Watch(inputId)
                            .Select(chg => chg.ToChangeSet()))
                        .RemoveKey();

                var obs = new ObservableInstance(created, instrumentedCall, inputs);
                mParent.mObservableInstances.AddOrUpdate(obs);

                IInstrumentedCall AddPlaceholderCall()
                {
                    return new InstrumentedCall(instrumentationPoint, cUnknownModule, cUnknown, cUnknown, cUnknown, 0);
                }
            }

            public void RelateObservableInstances(long inputObsId, long outputObsId)
            {
                mParent.GetInputsCache(outputObsId).AddOrUpdate(inputObsId);
            }
        }
    }
}
