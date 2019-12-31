using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewObservableInstance
    {
        public NewObservableInstance(EventInfo created, int instrumentedCallId)
        {
            Created = created;
            InstrumentedCallId = instrumentedCallId;
        }

        public EventInfo Created { get; }
        public int InstrumentedCallId { get; }
    }
}
