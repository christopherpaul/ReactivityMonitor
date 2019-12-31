using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model.ModelUpdate
{
    public sealed class NewSubscription
    {
        public NewSubscription(EventInfo eventInfo, long observableId)
        {
            Subscribed = eventInfo;
            ObservableId = observableId;
        }

        public EventInfo Subscribed { get; }
        public long ObservableId { get; }
    }
}
