using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility.Flyweights
{
    public static class Boxes
    {
        public static object True { get; } = true;
        public static object False { get; } = false;
        public static object Zero { get; } = 0;

        public static object For(bool x) => x ? True : False;
    }
}
