using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Utility.Extensions
{
    public static class ChangeExtensions
    {
        public static IChangeSet<TItem, TKey> ToChangeSet<TItem, TKey>(this Change<TItem, TKey> change)
        {
            return new ChangeSet<TItem, TKey>(1) { change };
        }

        public static IChangeSet<TItem> ToChangeSet<TItem>(this Change<TItem> change)
        {
            return new ChangeSet<TItem>() { change };
        }
    }
}
