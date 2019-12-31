using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewObservableInstanceLink
    {
        public NewObservableInstanceLink(long inputId, long outputId)
        {
            InputId = inputId;
            OutputId = outputId;
        }

        public long InputId { get; }
        public long OutputId { get; }
    }
}
