using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface ISourceMethod : IEquatable<ISourceMethod>
    {
        IModule Module { get; }
        string ParentType { get; }
        string Name { get; }
    }
}
