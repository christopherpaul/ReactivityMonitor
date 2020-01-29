using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support
{
    internal static class ProfilerOptions
    {
        static ProfilerOptions()
        {
            PipeName = Environment.GetEnvironmentVariable("REACTIVITYPROFILER_PIPENAME");
            WaitForConnection = IsTruthy(Environment.GetEnvironmentVariable("REACTIVITYPROFILER_WAITFORCONNECTION"));
            MonitorAllFromStart = IsTruthy(Environment.GetEnvironmentVariable("REACTIVITYPROFILER_MONITORALLFROMSTART"));
        }

        public static string PipeName { get; }
        public static bool WaitForConnection { get; }
        public static bool MonitorAllFromStart { get; }

        private static bool IsTruthy(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            if (int.TryParse(s, out int i))
            {
                return i != 0;
            }

            if (bool.TryParse(s, out bool b))
            {
                return b;
            }

            return true;
        }
    }
}
