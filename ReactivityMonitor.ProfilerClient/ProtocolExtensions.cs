using Model = ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Text;
using Protocol = ReactivityProfiler.Protocol;

namespace ReactivityMonitor.ProfilerClient
{
    internal static class ProtocolExtensions
    {
        public static Model.EventInfo ToModel(this Protocol.EventInfo info)
        {
            return new Model.EventInfo(info.SequenceId, new DateTime(info.Timestamp, DateTimeKind.Utc), info.ThreadId);
        }
    }
}
