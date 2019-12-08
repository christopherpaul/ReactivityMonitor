using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility
{
    public static class Comparer<TItem>
    {
        public static IComparer<TItem> ByKey<TKey>(Func<TItem, TKey> keySelector, IComparer<TKey> keyComparer)
        {
            return System.Collections.Generic.Comparer<TItem>.Create((a, b) => keyComparer.Compare(keySelector(a), keySelector(b)));
        }

        public static IComparer<TItem> ByKey<TKey>(Func<TItem, TKey> keySelector)
            where TKey : IComparable<TKey>
        {
            return System.Collections.Generic.Comparer<TItem>.Create((a, b) => keySelector(a).CompareTo(keySelector(b)));
        }
    }
}
