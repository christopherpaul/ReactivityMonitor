using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewTypeInfo
    {
        public NewTypeInfo(int typeId, string typeName, IReadOnlyList<string> propertyNames)
        {
            TypeId = typeId;
            TypeName = typeName;
            PropertyNames = propertyNames;
        }

        public int TypeId { get; }
        public string TypeName { get; }
        public IReadOnlyList<string> PropertyNames { get; }
    }
}
