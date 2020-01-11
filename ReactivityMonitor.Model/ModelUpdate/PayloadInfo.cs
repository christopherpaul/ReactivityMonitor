using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public abstract class PayloadInfo
    {
        protected PayloadInfo(int typeId)
        {
            TypeId = typeId;
        }

        public int TypeId { get; }
    }

    public sealed class SimplePayloadInfo : PayloadInfo
    {
        public SimplePayloadInfo(int typeId, object value) : base(typeId)
        {
            Value = value;
        }

        public object Value { get; }
    }

    public sealed class ObjectPayloadInfo : PayloadInfo
    {
        public ObjectPayloadInfo(int typeId, long objectId, string representation, int? itemCount) : base(typeId)
        {
            ObjectId = objectId;
            Representation = representation;
            ItemCount = itemCount;
        }

        public long ObjectId { get; }
        public string Representation { get; }
        public int? ItemCount { get; }
    }
}
