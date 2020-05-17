//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Permissions;
using System.Text;

namespace ReactivityMonitor.ProfilerClient.Attach
{
    #region internal classes 
    public class ProcessSafeHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private ProcessSafeHandle()
            : base(true)
        {
        }

        private ProcessSafeHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    public static class NativeMethods
    {
        private const string Kernel32LibraryName = "kernel32.dll";
        private const string Ole32LibraryName = "ole32.dll";
        private const string ShlwapiLibraryName = "shlwapi.dll";
        private const string ShimLibraryName = "mscoree.dll";

        public const int MAX_PATH = 260;


        [
         System.Runtime.ConstrainedExecution.ReliabilityContract(System.Runtime.ConstrainedExecution.Consistency.WillNotCorruptState, System.Runtime.ConstrainedExecution.Cer.Success),
         DllImport(Kernel32LibraryName)
        ]
        public static extern bool CloseHandle(IntPtr handle);


        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern void GetVersionFromProcess(ProcessSafeHandle hProcess, StringBuilder versionString,
                                                        Int32 bufferSize, out Int32 dwLength);

        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern void GetRequestedRuntimeVersion(string pExe, StringBuilder pVersion,
                                                             Int32 cchBuffer, out Int32 dwLength);

        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern void CLRCreateInstance(ref Guid clsid, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)]out object metahostInterface);

        public enum ProcessAccessOptions : int
        {
            ProcessTerminate = 0x0001,
            ProcessCreateThread = 0x0002,
            ProcessSetSessionID = 0x0004,
            ProcessVMOperation = 0x0008,
            ProcessVMRead = 0x0010,
            ProcessVMWrite = 0x0020,
            ProcessDupHandle = 0x0040,
            ProcessCreateProcess = 0x0080,
            ProcessSetQuota = 0x0100,
            ProcessSetInformation = 0x0200,
            ProcessQueryInformation = 0x0400,
            ProcessSuspendResume = 0x0800,
            Synchronize = 0x100000,
        }

        [
         DllImport(Kernel32LibraryName, PreserveSig = true)
        ]
        public static extern ProcessSafeHandle OpenProcess(Int32 dwDesiredAccess, bool bInheritHandle, Int32 dwProcessId);
#if false 

        [
         DllImport(Kernel32LibraryName, CharSet = CharSet.Unicode, PreserveSig = true)
        ]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(ProcessSafeHandle hProcess,
                                                            int dwFlags,
                                                            StringBuilder lpExeName,
                                                            ref int lpdwSize);

        [
         DllImport(Ole32LibraryName, PreserveSig = false)
        ]
        public static extern void CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter,
                                                   Int32 dwClsContext,
                                                   ref Guid riid, // must use "typeof(ICorDebug).GUID"
                                                   [MarshalAs(UnmanagedType.Interface)]out ICorDebug debuggingInterface
                                                   );

        public enum Stgm
        {
            StgmRead = 0x00000000,
            StgmWrite = 0x00000001,
            StgmReadWrite = 0x00000002,
            StgmShareDenyNone = 0x00000040,
            StgmShareDenyRead = 0x00000030,
            StgmShareDenyWrite = 0x00000020,
            StgmShareExclusive = 0x00000010,
            StgmPriority = 0x00040000,
            StgmCreate = 0x00001000,
            StgmConvert = 0x00020000,
            StgmFailIfThere = 0x00000000,
            StgmDirect = 0x00000000,
            StgmTransacted = 0x00010000,
            StgmNoScratch = 0x00100000,
            StgmNoSnapshot = 0x00200000,
            StgmSimple = 0x08000000,
            StgmDirectSwmr = 0x00400000,
            StgmDeleteOnRelease = 0x04000000
        }

        // SHCreateStreamOnFile* is used to create IStreams to pass to ICLRMetaHostPolicy::GetRequestedRuntime().
        // Since we can't count on the EX version being available, we have SHCreateStreamOnFile as a fallback.
        [
         DllImport(ShlwapiLibraryName, PreserveSig = false)
        ]
        // Only in version 6 and later
        public static extern void SHCreateStreamOnFileEx([MarshalAs(UnmanagedType.LPWStr)]string file,
                                                        Stgm dwMode,
                                                        Int32 dwAttributes, // Used if a file is created.  Identical to dwFlagsAndAttributes param of CreateFile.
                                                        bool create,
                                                        IntPtr pTemplate,   // Reserved, always pass null.
                                                        [MarshalAs(UnmanagedType.Interface)]out IStream openedStream);

        [
         DllImport(ShlwapiLibraryName, PreserveSig = false)
        ]
        public static extern void SHCreateStreamOnFile(string file,
                                                        Stgm dwMode,
                                                        [MarshalAs(UnmanagedType.Interface)]out IStream openedStream);

#endif
    }

    // Wrapper for ICLRMetaHost.  Used to find information about runtimes.
    public sealed class CLRMetaHost
    {
        private ICLRMetaHost m_metaHost;

        public const int MaxVersionStringLength = 26; // 24 + NULL and an extra
        private static readonly Guid clsidCLRMetaHost = new Guid("9280188D-0E8E-4867-B30C-7FA83884E8DE");

        public CLRMetaHost()
        {
            object o;
            Guid ifaceId = typeof(ICLRMetaHost).GUID;
            Guid clsid = clsidCLRMetaHost;
            NativeMethods.CLRCreateInstance(ref clsid, ref ifaceId, out o);
            m_metaHost = (ICLRMetaHost)o;
        }

        public CLRRuntimeInfo GetInstalledRuntimeByVersion(string version)
        {
            IEnumerable<CLRRuntimeInfo> runtimes = EnumerateInstalledRuntimes();

            foreach (CLRRuntimeInfo rti in runtimes)
            {
                if (rti.GetVersionString().ToString().ToLower() == version.ToLower())
                {
                    return rti;
                }
            }

            return null;
        }

        public CLRRuntimeInfo GetLoadedRuntimeByVersion(Int32 processId, string version)
        {
            IEnumerable<CLRRuntimeInfo> runtimes = EnumerateLoadedRuntimes(processId);

            foreach (CLRRuntimeInfo rti in runtimes)
            {
                if (rti.GetVersionString().Equals(version, StringComparison.OrdinalIgnoreCase))
                {
                    return rti;
                }
            }

            return null;
        }

        // Retrieve information about runtimes installed on the machine (i.e. in %WINDIR%\Microsoft.NET\)
        public IEnumerable<CLRRuntimeInfo> EnumerateInstalledRuntimes()
        {
            List<CLRRuntimeInfo> runtimes = new List<CLRRuntimeInfo>();
            IEnumUnknown enumRuntimes = m_metaHost.EnumerateInstalledRuntimes();

            // Since we're only getting one at a time, we can pass NULL for count.
            // S_OK also means we got the single element we asked for.
            for (object oIUnknown; enumRuntimes.Next(1, out oIUnknown, IntPtr.Zero) == 0; /* empty */)
            {
                runtimes.Add(new CLRRuntimeInfo(oIUnknown));
            }

            return runtimes;
        }

        // Retrieve information about runtimes that are currently loaded into the target process.
        public IEnumerable<CLRRuntimeInfo> EnumerateLoadedRuntimes(Int32 processId)
        {
            List<CLRRuntimeInfo> runtimes = new List<CLRRuntimeInfo>();
            IEnumUnknown enumRuntimes;

            using (ProcessSafeHandle hProcess = NativeMethods.OpenProcess(
                /*
                (int)(NativeMethods.ProcessAccessOptions.ProcessVMRead |
                                                                        NativeMethods.ProcessAccessOptions.ProcessQueryInformation |
                                                                        NativeMethods.ProcessAccessOptions.ProcessDupHandle |
                                                                        NativeMethods.ProcessAccessOptions.Synchronize),
                 */
                // TODO FIX NOW for debugging. 
                0x1FFFFF, // PROCESS_ALL_ACCESS
                                                                        false, // inherit handle
                                                                        processId))
            {
                if (hProcess.IsInvalid)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                enumRuntimes = m_metaHost.EnumerateLoadedRuntimes(hProcess);
            }

            // Since we're only getting one at a time, we can pass NULL for count.
            // S_OK also means we got the single element we asked for.
            for (object oIUnknown; enumRuntimes.Next(1, out oIUnknown, IntPtr.Zero) == 0; /* empty */)
            {
                runtimes.Add(new CLRRuntimeInfo(oIUnknown));
            }

            return runtimes;
        }

        public CLRRuntimeInfo GetRuntime(string version)
        {
            Guid ifaceId = typeof(ICLRRuntimeInfo).GUID;
            return new CLRRuntimeInfo(m_metaHost.GetRuntime(version, ref ifaceId));
        }
    }

    // You're expected to get this interface from mscoree!GetCLRMetaHost.
    // Details for APIs are in metahost.idl.
    [ComImport, InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), Guid("D332DB9E-B9B3-4125-8207-A14884F53216")]
    internal interface ICLRMetaHost
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        System.Object GetRuntime(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwzVersion,
            [In] ref Guid riid /*must use typeof(ICLRRuntimeInfo).GUID*/);

        void GetVersionFromFile(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwzFilePath,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
            [In, Out] ref uint pcchBuffer);

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumUnknown EnumerateInstalledRuntimes();

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumUnknown EnumerateLoadedRuntimes(
            [In] ProcessSafeHandle hndProcess);
    }

    // Wrapper for ICLRRuntimeInfo.  Represents information about a CLR install instance.
    public sealed class CLRRuntimeInfo
    {
        public CLRRuntimeInfo(System.Object clrRuntimeInfo)
        {
            m_runtimeInfo = (ICLRRuntimeInfo)clrRuntimeInfo;
        }

        public string GetVersionString()
        {
            StringBuilder sb = new StringBuilder(CLRMetaHost.MaxVersionStringLength);
            int verStrLength = sb.Capacity;
            m_runtimeInfo.GetVersionString(sb, ref verStrLength);
            return sb.ToString();
        }
        public string GetRuntimeDirectory()
        {
            StringBuilder sb = new StringBuilder();
            int strLength = 0;
            m_runtimeInfo.GetRuntimeDirectory(sb, ref strLength);
            sb.Capacity = strLength;
            int ret = m_runtimeInfo.GetRuntimeDirectory(sb, ref strLength);
            if (ret < 0)
            {
                Marshal.ThrowExceptionForHR(ret);
            }

            return sb.ToString();
        }

        public ICLRProfiling GetProfilingInterface()
        {
            Guid ifaceId = typeof(ICLRProfiling).GUID;
            Guid clsId = s_ClsIdClrProfiler;
            return (ICLRProfiling)m_runtimeInfo.GetInterface(ref clsId, ref ifaceId);
        }

        #region private 
        private static Guid s_ClsIdClrDebuggingLegacy = new Guid("DF8395B5-A4BA-450b-A77C-A9A47762C520");
        private static Guid s_ClsIdClrProfiler = new Guid("BD097ED8-733E-43FE-8ED7-A95FF9A8448C");
        private static Guid s_CorMetaDataDispenser = new Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8");

        private ICLRRuntimeInfo m_runtimeInfo;
        #endregion 
    }

    // Details about this interface are in metahost.idl.
    [ComImport, InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")]
    internal interface ICLRRuntimeInfo
    {
        // Marshalling pcchBuffer as int even though it's unsigned. Max version string is 24 characters, so we should not need to go over 2 billion soon.
        void GetVersionString([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
                              [In, Out, MarshalAs(UnmanagedType.U4)] ref int pcchBuffer);

        // Marshalling pcchBuffer as int even though it's unsigned. MAX_PATH is 260, unicode paths are 65535, so we should not need to go over 2 billion soon.
        [PreserveSig]
        int GetRuntimeDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
                                [In, Out, MarshalAs(UnmanagedType.U4)] ref int pcchBuffer);

        int IsLoaded([In] IntPtr hndProcess);

        // Marshal pcchBuffer as int even though it's unsigned. Error strings approaching 2 billion characters are currently unheard-of.
        [LCIDConversion(3)]
        void LoadErrorString([In, MarshalAs(UnmanagedType.U4)] int iResourceID,
                             [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
                             [In, Out, MarshalAs(UnmanagedType.U4)] ref int pcchBuffer,
                             [In] int iLocaleID);

        IntPtr LoadLibrary([In, MarshalAs(UnmanagedType.LPWStr)] string pwzDllName);

        IntPtr GetProcAddress([In, MarshalAs(UnmanagedType.LPStr)] string pszProcName);

        [return: MarshalAs(UnmanagedType.IUnknown)]
        System.Object GetInterface([In] ref Guid rclsid, [In] ref Guid riid);

    }

    // Wrapper for standard COM IEnumUnknown, needed for ICLRMetaHost enumeration APIs.
    [ComImport, InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), Guid("00000100-0000-0000-C000-000000000046")]
    internal interface IEnumUnknown
    {

        [PreserveSig]
        int Next(
            [In, MarshalAs(UnmanagedType.U4)]
             int celt,
            [Out, MarshalAs(UnmanagedType.IUnknown)]
            out System.Object rgelt,
            IntPtr pceltFetched);

        [PreserveSig]
        int Skip(
        [In, MarshalAs(UnmanagedType.U4)]
            int celt);

        void Reset();

        void Clone(
            [Out]
            out IEnumUnknown ppenum);
    }

    #endregion
} /* namespace */
