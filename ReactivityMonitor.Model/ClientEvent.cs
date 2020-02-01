using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class ClientEvent
    {
        public ClientEvent(EventInfo info, string description)
        {
            Info = info;
            Description = description;
        }

        public EventInfo Info { get; }
        public string Description { get; }
    }
}
