using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class ObjectDataRequest
    {
        public ObjectDataRequest(long objectId)
        {
            ObjectId = objectId;
        }

        public long ObjectId { get; }
    }
}
