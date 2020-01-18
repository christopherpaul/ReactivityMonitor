using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
#if true
    /// <summary>
    /// This class isn't used; it was the basis for the IL injected to handle assembly
    /// resolve events.
    /// </summary>
    static class DummySupportAssemblyResolution
    {
        [SecuritySafeCritical]
        static DummySupportAssemblyResolution()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSupportAssembly;
        }

        public static void EnsureHandler() { }

        private static Assembly ResolveSupportAssembly(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            string path = @"E:\Documents\programming\Repos\ReactivityMonitor\ReactivityMonitor\bin\Debug\profiler\x64\" + name.Name + ".dll";
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }
    }
#endif
}
