﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Connection
{
    [Serializable]
    internal class ConnectionException : Exception
    {
        public ConnectionException(string message) : base(message)
        {
        }

        public ConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
