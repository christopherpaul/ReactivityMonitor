using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class TypeInfoStore
    {
        private readonly ConcurrentDictionary<Type, (int, string[])> mTypeIds = new ConcurrentDictionary<Type, (int, string[])>();
        private readonly Func<Type, (int, string[])> mCreateNewTypeId;
        private int mFreshTypeIdSource;

        public TypeInfoStore(Action<Protocol.Type> notifyNewType)
        {
            mCreateNewTypeId = type =>
            {
                int id = Interlocked.Increment(ref mFreshTypeIdSource);
                var info = new Protocol.Type
                {
                    TypeId = id,
                    TypeName = type.FullName
                };
                string[] propertyNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanRead)
                        .Select(p => p.Name)
                        .ToArray();
                info.PropertyNames.AddRange(propertyNames);
                notifyNewType(info);

                return (id, propertyNames);
            };
        }

        public int GetTypeId(Type type)
        {
            (int id, _) = mTypeIds.GetOrAdd(type, mCreateNewTypeId);
            return id;
        }

        public IReadOnlyList<string> GetProperties(Type type)
        {
            (_, string[] propNames) = mTypeIds.GetOrAdd(type, mCreateNewTypeId);
            return propNames;
        }
    }
}
