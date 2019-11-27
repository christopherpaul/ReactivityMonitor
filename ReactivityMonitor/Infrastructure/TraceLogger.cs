using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Infrastructure
{
    class TraceLogger : ILog
    {
        public void Error(Exception exception)
        {
            Trace.TraceError("{0}", exception);
        }

        public void Info(string format, params object[] args)
        {
            Trace.TraceInformation(format, args);
        }

        public void Warn(string format, params object[] args)
        {
            Trace.TraceWarning(format, args);
        }
    }
}
