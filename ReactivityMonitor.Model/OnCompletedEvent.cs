﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public sealed class OnCompletedEvent : StreamEvent
    {
        public OnCompletedEvent(EventInfo info) : base(info)
        {
        }

        public override EventKind Kind => EventKind.OnCompleted;
    }
}
