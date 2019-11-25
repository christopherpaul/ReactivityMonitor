using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support
{
    internal static class Services
    {
        public static Store.IStore Store { get; } = new Store.Store();
    }
}
