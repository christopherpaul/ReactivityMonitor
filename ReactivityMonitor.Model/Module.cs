using System;
using System.Collections.Generic;
using System.Text;
using DynamicData;

namespace ReactivityMonitor.Model
{
    internal sealed class Module : IModule
    {
        public Module(ulong id, string path, string assemblyName, IObservable<IInstrumentedCall> instrumentedCalls)
        {
            ModuleId = id;
            Path = path;
            AssemblyName = assemblyName;
            InstrumentedCalls = instrumentedCalls;
        }

        public ulong ModuleId { get; }
        public string Path { get; }
        public string AssemblyName { get; }
        public IObservable<IInstrumentedCall> InstrumentedCalls { get; }
    }
}
