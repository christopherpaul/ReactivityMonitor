using DynamicData;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class PayloadObject
    {
        private readonly string mRepresentation;

        public PayloadObject(string typeName, long objectId, string representation, bool isExceptionGettingValue, int? itemCount, IObservable<IImmutableDictionary<string, object>> properties, IObservable<IImmutableList<object>> items)
        {
            TypeName = typeName;
            ObjectId = objectId;
            mRepresentation = representation;
            IsExceptionGettingValue = isExceptionGettingValue;
            ItemCount = itemCount;
            Properties = properties;
            Items = items;
        }

        public string TypeName { get; }
        public long ObjectId { get; }
        public bool IsExceptionGettingValue { get; }
        public int? ItemCount { get; }
        public IObservable<IImmutableDictionary<string, object>> Properties { get; }
        public IObservable<IImmutableList<object>> Items { get; }

        public override string ToString() => mRepresentation;
    }
}
