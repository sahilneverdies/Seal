

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Seal {

    public sealed class MemoryStats {
        public ulong TotalBytes;
        public ulong AvailBytes;
        public ulong UsedBytes;
        public double TotalGb { get { return TotalBytes / (1024.0 * 1024 * 1024); } }
        public double UsedGb { get { return UsedBytes / (1024.0 * 1024 * 1024); } }
        public double FreeGb { get { return AvailBytes / (1024.0 * 1024 * 1024); } }
        public int UsedPercent;
        public int FreePercent { get { return Math.Max(0, 100 - UsedPercent); } }
    }

    [Flags]
    public enum MemoryAreas {
        None = 0,
        WorkingSet = 1 << 0,
        SystemFileCache = 1 << 1,
        StandbyList = 1 << 2,
        ModifiedPageList = 1 << 3,
        CombinedPageList = 1 << 4,
        RegistryCache = 1 << 5,
        All = WorkingSet | SystemFileCache | StandbyList | ModifiedPageList | CombinedPageList | RegistryCache
    }

    public static class MemoryCleaner {
        [DllImport("ntdll.dll")]
        static extern int NtSetSystemInformation(int infoClass, IntPtr info, int length);

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetSystemFileCacheSize(IntPtr minimumFileCacheSize, IntPtr maximumFileCacheSize, int flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MEMORYSTATUSEX {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TOKEN_PRIVILEGES {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        static bool privilegesAcquired;
        static readonly object sync = new object();

        static void EnsurePrivileges() {
            if (privilegesAcquired) return;
            lock (sync) {
                if (privilegesAcquired) return;
                EnablePrivilege("SeIncreaseQuotaPrivilege");
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                privilegesAcquired = true;
            }
        }

        static bool EnablePrivilege(string privilege) {
            IntPtr hToken;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out hToken)) return false;
            try {
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Attributes = 0x00000002;
                if (!LookupPrivilegeValue(null, privilege, out tp.Luid)) return false;
                return AdjustTokenPrivileges(hToken, false, ref tp, Marshal.SizeOf(tp), IntPtr.Zero, IntPtr.Zero);
            } catch {
                return false;
            } finally {
                CloseHandle(hToken);
            }
        }

        public static MemoryStats GetStats() {
            var m = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(m);
            return new MemoryStats {
                TotalBytes = m.ullTotalPhys,
                AvailBytes = m.ullAvailPhys,
                UsedBytes = m.ullTotalPhys > m.ullAvailPhys ? m.ullTotalPhys - m.ullAvailPhys : 0,
                UsedPercent = (int)m.dwMemoryLoad
            };
        }

        public static long Optimize(MemoryAreas areas, out int procCount) {
            EnsurePrivileges();
            procCount = 0;
            var before = GetStats();


            if ((areas & MemoryAreas.WorkingSet) != 0) {
                var procs = Process.GetProcesses();
                foreach (var p in procs) {
                    try {
                        if (EmptyWorkingSet(p.Handle) != 0) procCount++;
                    } catch { }
                    finally { p.Dispose(); }
                }


                IntPtr pCmd = Marshal.AllocHGlobal(sizeof(int));
                try {
                    Marshal.WriteInt32(pCmd, 2);
                    NtSetSystemInformation(80, pCmd, sizeof(int));
                } catch { }
                finally { Marshal.FreeHGlobal(pCmd); }
            }


            if ((areas & MemoryAreas.SystemFileCache) != 0) {
                try { SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0); } catch { }
            }


            IntPtr pListCmd = Marshal.AllocHGlobal(sizeof(int));
            try {
                if ((areas & MemoryAreas.StandbyList) != 0) {
                    Marshal.WriteInt32(pListCmd, 4);
                    NtSetSystemInformation(80, pListCmd, sizeof(int));
                    Marshal.WriteInt32(pListCmd, 5);
                    NtSetSystemInformation(80, pListCmd, sizeof(int));
                }
                if ((areas & MemoryAreas.ModifiedPageList) != 0) {
                    Marshal.WriteInt32(pListCmd, 3);
                    NtSetSystemInformation(80, pListCmd, sizeof(int));
                }
                if ((areas & MemoryAreas.CombinedPageList) != 0) {
                    Marshal.WriteInt32(pListCmd, 6);
                    NtSetSystemInformation(80, pListCmd, sizeof(int));
                }
            } catch { }
            finally { Marshal.FreeHGlobal(pListCmd); }

            var after = GetStats();
            long freed = after.AvailBytes > before.AvailBytes ? (long)(after.AvailBytes - before.AvailBytes) : 0;
            Log.Write(string.Format("Memory optimized: freed {0:F0} MB ({1} processes, areas {2})",
                freed / (1024.0 * 1024.0), procCount, areas));
            return freed;
        }
    }
}
