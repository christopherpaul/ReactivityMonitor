using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class ReactivityModelSource
    {
        private readonly ISourceCache<IModule, ulong> mModules;
        private readonly ISourceCache<IInstrumentedCall, int> mInstrumentedCalls;

        private readonly ISourceCache<int, int> mRequestedInstrumentedCalls;

        public ReactivityModelSource()
        {
            mModules = new SourceCache<IModule, ulong>(m => m.ModuleId);
            mInstrumentedCalls = new SourceCache<IInstrumentedCall, int>(ic => ic.InstrumentedCallId);
            mRequestedInstrumentedCalls = new SourceCache<int, int>(id => id);

            Model = new ModelImpl(this);
            Updater = new UpdaterImpl(this);
            ProfilerControl = new ProfilerControlImpl(this);
        }

        public IReactivityModel Model { get; }
        public IModelUpdater Updater { get; }
        public IProfilerControl ProfilerControl { get; }

        private sealed class ModelImpl : IReactivityModel
        {
            private readonly ReactivityModelSource mParent;

            public ModelImpl(ReactivityModelSource parent)
            {
                Modules = parent.mModules.AsObservableCache();
                InstrumentedCalls = parent.mInstrumentedCalls.AsObservableCache();
                mParent = parent;
            }

            public IObservableCache<IModule, ulong> Modules { get; }

            public IObservableCache<IInstrumentedCall, int> InstrumentedCalls { get; }

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
        }
    }
}
