using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class ObjectPropertiesInfo
    {
        public ObjectPropertiesInfo(long objectId, IImmutableList<PayloadInfo> propertyValues)
        {
            ObjectId = objectId;
            PropertyValues = propertyValues;
        }

        public long ObjectId { get; }
        public IImmutableList<PayloadInfo> PropertyValues { get; }
    }
}
