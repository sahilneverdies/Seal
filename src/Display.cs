

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Seal {

    public static class Display {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct DEVMODE {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        const int ENUM_CURRENT_SETTINGS = -1, DM_DISPLAYFREQUENCY = 0x400000, CDS_UPDATEREGISTRY = 1, DISP_CHANGE_SUCCESSFUL = 0;
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int ChangeDisplaySettingsEx(string deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] struct LUID { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] struct PathSource { public LUID adapter; public uint id, modeIdx, statusFlags; }
        [StructLayout(LayoutKind.Sequential)] struct PathTarget {
            public LUID adapter; public uint id, modeIdx, outputTechnology, rotation, scaling, refreshNum, refreshDen, scanLineOrdering;
            public int targetAvailable; public uint statusFlags;
        }
        [StructLayout(LayoutKind.Sequential)] struct PathInfo { public PathSource source; public PathTarget target; public uint flags; }
        [StructLayout(LayoutKind.Sequential)] struct ModeInfo {
            public uint infoType; public uint id; public LUID adapter;

            public long a, b, c, d, e, f;
        }
        [StructLayout(LayoutKind.Sequential)] struct DeviceInfoHeader { public uint type, size; public LUID adapter; public uint id; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct SourceDeviceName {
            public DeviceInfoHeader header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string gdiDeviceName;
        }
        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] PathInfo[] paths, ref uint numModes, [Out] ModeInfo[] modes, IntPtr topologyId);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName req);
        const uint QDC_ONLY_ACTIVE_PATHS = 2, GET_SOURCE_NAME = 1;

        const uint TECH_INTERNAL = 0x80000000, TECH_DISPLAYPORT_EMBEDDED = 11, TECH_UDI_EMBEDDED = 13;

        static string Panel() {
            try {
                uint nPaths, nModes;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out nPaths, out nModes) != 0) return null;
                var paths = new PathInfo[nPaths];
                var modes = new ModeInfo[nModes];
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref nPaths, paths, ref nModes, modes, IntPtr.Zero) != 0) return null;
                for (int i = 0; i < nPaths; i++) {
                    uint tech = paths[i].target.outputTechnology;
                    if (tech != TECH_INTERNAL && tech != TECH_DISPLAYPORT_EMBEDDED && tech != TECH_UDI_EMBEDDED) continue;
                    var q = new SourceDeviceName();
                    q.header.type = GET_SOURCE_NAME;
                    q.header.size = (uint)Marshal.SizeOf(typeof(SourceDeviceName));
                    q.header.adapter = paths[i].source.adapter;
                    q.header.id = paths[i].source.id;
                    if (DisplayConfigGetDeviceInfo(ref q) != 0) continue;
                    if (!string.IsNullOrEmpty(q.gdiDeviceName)) return q.gdiDeviceName;
                }
            } catch (Exception ex) { Log.Write("internal panel: " + ex.Message); }
            return null;
        }

        static DEVMODE Fresh() { var d = new DEVMODE(); d.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)); return d; }


        public static int CurrentHz() {
            try { string p = Panel(); var d = Fresh(); if (EnumDisplaySettings(p, ENUM_CURRENT_SETTINGS, ref d)) return d.dmDisplayFrequency; } catch { }
            return 0;
        }


        public static int[] Rates() {
            var set = new SortedDictionary<int, bool>();
            try {
                string p = Panel();
                var cur = Fresh();
                if (!EnumDisplaySettings(p, ENUM_CURRENT_SETTINGS, ref cur)) return new int[0];
                for (int i = 0; ; i++) {
                    var d = Fresh();
                    if (!EnumDisplaySettings(p, i, ref d)) break;
                    if (d.dmPelsWidth == cur.dmPelsWidth && d.dmPelsHeight == cur.dmPelsHeight && d.dmBitsPerPel == cur.dmBitsPerPel && d.dmDisplayFrequency > 1) set[d.dmDisplayFrequency] = true;
                }
            } catch (Exception ex) { Log.Write("display modes: " + ex.Message); }
            var r = new List<int>(set.Keys);
            return r.ToArray();
        }


        public static int[] Choices() {
            var all = Rates();
            if (all.Length < 2) return new int[0];
            var pick = new SortedDictionary<int, bool>();
            int max = all[all.Length - 1];
            pick[max] = true;
            foreach (int r in all) if (r == 60) pick[60] = true;
            foreach (int r in all) if (r >= 48) { pick[r] = true; break; }
            var l = new List<int>(pick.Keys);
            return l.ToArray();
        }

        public static int BatteryHz() {
            var all = Rates();
            if (all.Length == 0) return 0;
            foreach (int r in all) if (r == 60) return 60;
            foreach (int r in all) if (r >= 48) return r;
            return all[0];
        }


        public static int HighestHz() {
            var all = Rates();
            return all.Length == 0 ? 0 : all[all.Length - 1];
        }

        public static bool SetHz(int hz) {
            try {
                string p = Panel();
                var d = Fresh();
                if (!EnumDisplaySettings(p, ENUM_CURRENT_SETTINGS, ref d)) return false;
                if (d.dmDisplayFrequency == hz) return true;
                d.dmDisplayFrequency = hz;
                d.dmFields = DM_DISPLAYFREQUENCY;
                int rc = ChangeDisplaySettingsEx(p, ref d, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                Log.Write("refresh rate " + hz + " Hz -> rc " + rc);
                return rc == DISP_CHANGE_SUCCESSFUL;
            } catch (Exception ex) { Log.Write("refresh rate: " + ex.Message); return false; }
        }


        public static void Off() {
            const int WM_SYSCOMMAND = 0x0112, SC_MONITORPOWER = 0xF170;
            var HWND_BROADCAST = new IntPtr(0xFFFF);
            try { SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(2)); } catch (Exception ex) { Log.Write("display off: " + ex.Message); }
        }
    }
}
