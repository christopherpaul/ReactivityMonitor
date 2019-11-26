using System;
using System.Collections.Generic;
using System.Text;
using DynamicData;

namespace ReactivityMonitor.Model
{
    internal sealed class Module : IModule
    {
        private readonly ISourceCache<IInstrumentedCall, int> mInstrumentedCalls;

        public Module(ulong id, string path)
        {
            ModuleId = id;
            Path = path;
            mInstrumentedCalls = new SourceCache<IInstrumentedCall, int>(ic => ic.InstrumentedCallId);
            InstrumentedCalls = mInstrumentedCalls.AsObservableCache();
        }

        public ulong ModuleId { get; }
        public string Path { get; }

        public IObservableCache<IInstrumentedCall, int> InstrumentedCalls { get; }

        internal void AddInstrumentedCall(IInstrumentedCall ic) => mInstrumentedCalls.AddOrUpdate(ic);
    }
}
