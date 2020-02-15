using System;
using System.Collections.Generic;
using System.Text;
using DynamicData;

namespace ReactivityMonitor.Model
{
    internal sealed class Module : IModule
    {
        public Module(ulong id, string path, string assemblyName, IObservable<IInstrumentedMethod> instrumentedMethods)
        {
            ModuleId = id;
            Path = path;
            AssemblyName = assemblyName;
            InstrumentedMethods = instrumentedMethods;
        }

        public ulong ModuleId { get; }
        public string Path { get; }
        public string AssemblyName { get; }
        public IObservable<IInstrumentedMethod> InstrumentedMethods { get; }
    }
}
