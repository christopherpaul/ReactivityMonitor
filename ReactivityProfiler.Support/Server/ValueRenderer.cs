using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class ValueRenderer
    {
        private readonly ConcurrentDictionary<Type, int> mTypeIds = new ConcurrentDictionary<Type, int>();
        private readonly Func<Type, int> mCreateNewTypeId;
        private readonly PayloadStore mStore;
        private int mFreshTypeIdSource;

        public ValueRenderer(PayloadStore store, Action<Protocol.Type> notifyNewType)
        {
            mCreateNewTypeId = type =>
            {
                int id = Interlocked.Increment(ref mFreshTypeIdSource);
                var info = new Protocol.Type
                {
                    TypeId = id,
                    TypeName = type.FullName
                };
                info.PropertyNames.AddRange(
                    type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanRead)
                        .Select(p => p.Name));
                notifyNewType(info);

                return id;
            };
            mStore = store;
        }

        public Protocol.Value GetPayloadValue<T>(T value)
        {
            Type type = value?.GetType() ?? typeof(object);
            int typeId = mTypeIds.GetOrAdd(type, mCreateNewTypeId);

            var renderedValue = new Protocol.Value
            {
                TypeId = typeId
            };

            if (value == null)
            {
                renderedValue.Null = true;
            }
            else if (type.IsPrimitive)
            {
                switch (value)
                {
                    case int x:
                        renderedValue.Int64 = x;
                        break;
                    case long x:
                        renderedValue.Int64 = x;
                        break;
                    case byte x:
                        renderedValue.Int64 = x;
                        break;
                    case sbyte x:
                        renderedValue.Int64 = x;
                        break;
                    case short x:
                        renderedValue.Int64 = x;
                        break;
                    case ushort x:
                        renderedValue.Int64 = x;
                        break;
                    case uint x:
                        renderedValue.Int64 = x;
                        break;
                    case IntPtr x:
                        renderedValue.Int64 = (long)x;
                        break;
                    case ulong x:
                        renderedValue.UInt64 = x;
                        break;
                    case UIntPtr x:
                        renderedValue.UInt64 = (ulong)x;
                        break;
                    case double x:
                        renderedValue.Double = x;
                        break;
                    case float x:
                        renderedValue.Double = x;
                        break;
                    case char x:
                        renderedValue.Char = x;
                        break;
                    case bool x:
                        renderedValue.Bool = x;
                        break;
                }
            }
            else
            {
                switch (value)
                {
                    case string x:
                        renderedValue.String = x;
                        break;
                    case DateTime x when x.Kind == DateTimeKind.Utc:
                        renderedValue.DateTimeUtc = x.Ticks;
                        break;
                    case DateTime x when x.Kind == DateTimeKind.Local:
                        renderedValue.DateTimeLocal = x.Ticks;
                        break;
                    case DateTime x when x.Kind == DateTimeKind.Unspecified:
                        renderedValue.DateTimeUnspecified = x.Ticks;
                        break;
                    case TimeSpan x:
                        renderedValue.Timespan = x.Ticks;
                        break;
                    case Guid x:
                        renderedValue.ByteString = Google.Protobuf.ByteString.CopyFrom(x.ToByteArray());
                        break;
                    case byte[] x when x.Length <= 256:
                        renderedValue.ByteString = Google.Protobuf.ByteString.CopyFrom(x);
                        break;
                    default:
                        renderedValue.Object = new Protocol.Object
                        {
                            ObjectId = mStore.Store(value),
                        };
                        try
                        {
                            if (value is System.Collections.ICollection list)
                            {
                                //TODO cater for collection types that don't implement ICollection but implement IReadOnlyCollection<>
                                renderedValue.Object.ItemCount = list.Count;
                            }
                            else if (value is System.Collections.IEnumerable)
                            {
                                renderedValue.Object.ItemCount = -1;
                            }
                            renderedValue.Object.StringRepresentation = value is Exception ex ? ex.Message : value.ToString();
                        }
                        catch { }
                        break;
                }
            }

            return renderedValue;
        }
    }
}
