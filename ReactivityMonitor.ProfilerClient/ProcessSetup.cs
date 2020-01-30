using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ReactivityMonitor.ProfilerClient
{
    public sealed class ProcessSetup
    {
        private readonly string mProfilerLocation32;
        private readonly string mProfilerLocation64;
        private readonly string mSupportAssemblyLocation;
        private readonly string mPipeName;

        public ProcessSetup(string profilersLocation)
        {
            const string cProfilerDll = "ReactivityProfiler.dll";
            mProfilerLocation32 = Path.Combine(profilersLocation, "Win32", cProfilerDll);
            mProfilerLocation64 = Path.Combine(profilersLocation, "x64", cProfilerDll);
            mSupportAssemblyLocation = Path.Combine(profilersLocation, "x64", "ReactivityProfiler.Support.dll");

            mPipeName = $"ReactivityProfiler.{Guid.NewGuid():N}";
        }

        public bool WaitForConnection { get; set; }
        public bool MonitorAllFromStart { get; set; }

        public string PipeName => mPipeName;

        public IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
        {
            IEnumerable<(string, string)> Generate()
            {
                foreach (string prefix in new[] { "COR", "CORECLR" })
                {
                    yield return ($"{prefix}_ENABLE_PROFILING", "1");
                    yield return ($"{prefix}_PROFILER_PATH_32", mProfilerLocation32);
                    yield return ($"{prefix}_PROFILER_PATH_64", mProfilerLocation64);
                    yield return ($"{prefix}_PROFILER", "{09c5b5d7-62d2-4448-911d-2e1346a21110}");
                }

                yield return ("DOTNET_STARTUP_HOOKS", mSupportAssemblyLocation);

                yield return ("REACTIVITYPROFILER_PIPENAME", mPipeName);

                yield return ("REACTIVITYPROFILER_WAITFORCONNECTION", WaitForConnection.ToString());
                yield return ("REACTIVITYPROFILER_MONITORALLFROMSTART", MonitorAllFromStart.ToString());
            }

            return Generate().Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2));
        }

        public void SetEnvironmentVariables(ProcessStartInfo psi)
        {
            foreach (var env in GetEnvironmentVariables())
            {
                psi.EnvironmentVariables.Add(env.Key, env.Value);
            }
        }
    }
}
