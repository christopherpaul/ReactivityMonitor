using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility.Flyweights
{
    public static class Funcs<T>
    {
        public static Func<T, T> Identity() => cIdentity;
        public static Func<U, T> Default<U>() => Taking<U>.Default;

        private static readonly Func<T, T> cIdentity = x => x;

        private static class Taking<U>
        {
            public static readonly Func<U, T> Default = _ => default;
        }
    }
}
