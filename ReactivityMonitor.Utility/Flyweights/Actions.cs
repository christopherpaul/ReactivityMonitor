using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility.Flyweights
{
    public static class Actions
    {
        public static Action NoOp { get; } = () => { };
    }

    public static class Actions<T>
    {
        public static Action<T> NoOp { get; } = _ => { };
    }
}
