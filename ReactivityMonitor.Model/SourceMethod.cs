using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    internal sealed class SourceMethod : ISourceMethod
    {
        public SourceMethod(IModule module, string parentType, string name)
        {
            Module = module;
            ParentType = parentType;
            Name = name;
        }

        public IModule Module { get; }
        public string ParentType { get; }
        public string Name { get; }

        private (ulong, string, string) EqualityKey => (Module.ModuleId, ParentType, Name);

        private bool Equals(SourceMethod other)
        {
            return other != null && EqualityKey.Equals(other.EqualityKey);
        }

        public bool Equals(ISourceMethod other) => Equals(other as SourceMethod);

        public override bool Equals(object obj) => Equals(obj as SourceMethod);

        public override int GetHashCode() => EqualityKey.GetHashCode();
    }
}
