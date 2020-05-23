using ReactivityProfiler.Support;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

public static class ProfilerStartupHook
{
    private static string sSupportAssemblyLocation;

    public static int Initialize(string dummy)
    {
        Debug.WriteLine($"{nameof(ProfilerStartupHook)}.{nameof(Initialize)}: setting up assembly resolution");
        SetUpAssemblyResolution();

        Debug.WriteLine($"{nameof(ProfilerStartupHook)}.{nameof(Initialize)}: initialising");
        Instrument.EnsureInitialised();

        return 0;
    }

    private static void SetUpAssemblyResolution()
    {
        sSupportAssemblyLocation = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        string path = Path.Combine(sSupportAssemblyLocation, args.Name + ".dll");
        if (File.Exists(path))
        {
            return Assembly.LoadFrom(path);
        }

        return null;
    }
}
