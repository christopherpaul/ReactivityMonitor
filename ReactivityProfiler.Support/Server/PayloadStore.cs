using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class PayloadStore
    {
        private long mFreshIdentifierSource;
        private readonly PerTypeStore<object> mRefTypeStore;
        private readonly ConcurrentDictionary<long, PerTypeStore> mStoreByIdentifier = new ConcurrentDictionary<long, PerTypeStore>();
        private readonly ConcurrentDictionary<Type, PerTypeStore> mStoreByValueType = new ConcurrentDictionary<Type, PerTypeStore>();

        public PayloadStore()
        {
            mRefTypeStore = new PerTypeStore<object>(this);
        }

        /// <summary>
        /// Stores <paramref name="value"/> and returns an identifier to use to refer
        /// to it. Does not support <c>null</c> or primitive type values. Avoid using
        /// for simple values that are better communicated in their entirety, such
        /// as strings, DateTimes.
        /// </summary>
        public long Store<T>(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var type = value.GetType();
            if (type.IsPrimitive)
            {
                throw new ArgumentException("Primitive type values are not allowed", nameof(value));
            }

            if (type.IsValueType)
            {
                // Each value type gets its own store to avoid boxing while storing.
                // value types can't be subtyped so type == typeof(T)
                PerTypeStore s;
                if (!mStoreByValueType.TryGetValue(type, out s))
                {
                    s = mStoreByValueType.GetOrAdd(type, new PerTypeStore<T>(this));
                }

                var store = (PerTypeStore<T>)s;
                return store.GetIdentifier(value);
            }
            else
            {
                var obj = (object)value;
                return mRefTypeStore.GetIdentifier(obj);
            }
        }

        public object Retrieve(long identifier)
        {
            if (!mStoreByIdentifier.TryGetValue(identifier, out PerTypeStore store))
            {
                throw new KeyNotFoundException();
            }

            return store.GetValue(identifier);
        }

        private abstract class PerTypeStore
        {
            public abstract object GetValue(long identifier);
        }

        private sealed class PerTypeStore<T> : PerTypeStore
        {
            private readonly ConcurrentDictionary<long, T> mValuesByIdentifier = new ConcurrentDictionary<long, T>();
            private readonly ConcurrentDictionary<T, long> mIdentifiersByValue = new ConcurrentDictionary<T, long>();
            private readonly Func<T, long> mGetFreshIdentifier;

            public PerTypeStore(PayloadStore parent)
            {
                mGetFreshIdentifier = value =>
                {
                    long identifier = Interlocked.Increment(ref parent.mFreshIdentifierSource);
                    mValuesByIdentifier.TryAdd(identifier, value);
                    parent.mStoreByIdentifier.TryAdd(identifier, this);

                    return identifier;
                };
            }

            public long GetIdentifier(T value)
            {
                long identifier = mIdentifiersByValue.GetOrAdd(value, mGetFreshIdentifier);
                return identifier;
            }

            public override object GetValue(long identifier)
            {
                if (!mValuesByIdentifier.TryGetValue(identifier, out T value))
                {
                    throw new KeyNotFoundException();
                }

                return value;
            }
        }
    }
}
