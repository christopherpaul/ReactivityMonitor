using DynamicData;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.Extensions
{
    public static class ChangeExtensions
    {
        public static IChangeSet<TItem, TKey> ToChangeSet<TItem, TKey>(this Change<TItem, TKey> change)
        {
            return new ChangeSet<TItem, TKey>(1) { change };
        }
    }
}
