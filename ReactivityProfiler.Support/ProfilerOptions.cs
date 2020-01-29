using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support
{
    internal static class ProfilerOptions
    {
        public static string PipeName => Environment.GetEnvironmentVariable("REACTIVITYPROFILER_PIPENAME");
        public static bool WaitForConnection => IsTruthy(Environment.GetEnvironmentVariable("REACTIVITYPROFILER_WAITFORCONNECTION"));

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
