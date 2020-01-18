using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

// Must be in global namespace
internal class StartupHook
{
    private static string sSupportAssemblyLocation;

    public static void Initialize()
    {
        sSupportAssemblyLocation = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
        AssemblyLoadContext.Default.Resolving += OnResolutionRequired;
    }

    private static Assembly OnResolutionRequired(AssemblyLoadContext context, AssemblyName name)
    {
        string path = Path.Combine(sSupportAssemblyLocation, name.Name + ".dll");
        if (File.Exists(path))
        {
            return Assembly.LoadFrom(path);
        }

        return null;
    }
}
