using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReactivityMonitor.ProfilerClient.Attach
{
    internal static class Native
    {
        internal static readonly uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        internal static readonly uint STANDARD_RIGHTS_READ = 0x00020000;
        internal static readonly uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        internal static readonly uint TOKEN_DUPLICATE = 0x0002;
        internal static readonly uint TOKEN_IMPERSONATE = 0x0004;
        internal static readonly uint TOKEN_QUERY = 0x0008;
        internal static readonly uint TOKEN_QUERY_SOURCE = 0x0010;
        internal static readonly uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        internal static readonly uint TOKEN_ADJUST_GROUPS = 0x0040;
        internal static readonly uint TOKEN_ADJUST_SESSIONID = 0x0100;
        internal static readonly uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);


        internal enum TOKEN_ELEVATION_TYPE
        {
            TokenElevationTypeDefault = 1,
            TokenElevationTypeFull = 2,
            TokenElevationTypeLimited = 3
        }

        internal enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups = 2,
            TokenPrivileges = 3,
            TokenOwner = 4,
            TokenPrimaryGroup = 5,
            TokenDefaultDacl = 6,
            TokenSource = 7,
            TokenType = 8,
            TokenImpersonationLevel = 9,
            TokenStatistics = 10,
            TokenRestrictedSids = 11,
            TokenSessionId = 12,
            TokenGroupsAndPrivileges = 13,
            TokenSessionReference = 14,
            TokenSandBoxInert = 15,
            TokenAuditPolicy = 16,
            TokenOrigin = 17,
            TokenElevationType = 18,
            TokenLinkedToken = 19,
            TokenElevation = 20,
            TokenHasRestrictions = 21,
            TokenAccessInformation = 22,
            TokenVirtualizationAllowed = 23,
            TokenVirtualizationEnabled = 24,
            TokenIntegrityLevel = 25,
            TokenUIAccess = 26,
            TokenMandatoryPolicy = 27,
            TokenLogonSid = 28,
            MaxTokenInfoClass = 29  // MaxTokenInfoClass should always be the last enum
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(
            [In] IntPtr ProcessHandle,
            [In] UInt32 DesiredAccess,
            [Out] out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            TOKEN_INFORMATION_CLASS TokenInformationClass,
            IntPtr TokenInformation,
            int TokenInformationLength,
            out int ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AdjustTokenPrivileges(
           [In] IntPtr TokenHandle,
           [In, MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
           [In] ref TOKEN_PRIVILEGES NewState,
           [In] UInt32 BufferLength,
           // [Out] out TOKEN_PRIVILEGES PreviousState,
           [In] IntPtr NullParam,
           [In] IntPtr ReturnLength);

        // I explicitly DONT capture GetLastError information on this call because it is often used to
        // clean up and it is cleaner if GetLastError still points at the orginal error, and not the failure
        // in CloseHandle.  If we ever care about exact errors of CloseHandle, we can make another entry
        // point 
        [DllImport("kernel32.dll")]
        internal static extern int CloseHandle([In] IntPtr hHandle);

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_PRIVILEGES      // taylored for the case where you only have 1. 
        {
            public UInt32 PrivilegeCount;
            public LUID Luid;
            public UInt32 Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID
        {
            public UInt32 LowPart;
            public Int32 HighPart;
        }

        // Constants for the Attributes field
        internal const UInt32 SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
        internal const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;
        internal const UInt32 SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

        // Constants for the Luid field 
        internal const uint SE_SYSTEM_PROFILE_PRIVILEGE = 11;
        internal const uint SE_DEBUG_PRIVILEGE = 20;


        // TODO what is this for?
        internal static int GetHRForLastWin32Error()
        {
            int dwLastError = Marshal.GetLastWin32Error();
            if ((dwLastError & 0x80000000) == 0x80000000)
            {
                return dwLastError;
            }
            else
            {
                return (dwLastError & 0x0000FFFF) | unchecked((int)0x80070000);
            }
        }

        internal static void SetPrivilege(uint privilege)
        {
#if !NOT_WINDOWS
            Process process = Process.GetCurrentProcess();
            IntPtr tokenHandle = IntPtr.Zero;
            bool success = OpenProcessToken(process.SafeHandle.DangerousGetHandle(), TOKEN_ADJUST_PRIVILEGES, out tokenHandle);
            if (!success)
            {
                throw new Win32Exception();
            }

            GC.KeepAlive(process);                      // TODO get on SafeHandles. 

            TOKEN_PRIVILEGES privileges = new TOKEN_PRIVILEGES();
            privileges.PrivilegeCount = 1;
            privileges.Luid.LowPart = privilege;
            privileges.Attributes = SE_PRIVILEGE_ENABLED;

            success = AdjustTokenPrivileges(tokenHandle, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
            CloseHandle(tokenHandle);
            if (!success)
            {
                throw new Win32Exception();
            }
#endif
        }
    }
}
