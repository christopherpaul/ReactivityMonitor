using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility.Flyweights
{
    public static class Funcs<T>
    {
        public static Func<T, T> Identity { get; } = x => x;
        public static Func<T> Default { get; } = () => default;
        public static Func<T, U> DefaultOf<U>() => For<U>.Default;
        public static Func<T, bool> True { get; } = _ => true;
        public static Func<T, bool> False { get; } = _ => false;

        private static class For<U>
        {
            public static readonly Func<T, U> Default = _ => default;
        }
    }

    public static class Funcs
    {
        public static Func<bool> True { get; } = () => true;
        public static Func<bool> False { get; } = () => false;
        public static Func<T> DefaultOf<T>() => Funcs<T>.Default;
        public static Func<T, T> Identity<T>() => Funcs<T>.Identity;
    }
}
