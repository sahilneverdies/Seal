using System;
using System.IO;
using System.Runtime.InteropServices;
using Seal;

class Program {
    [DllImport("hid.dll", SetLastError = true)]
    static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    static void Main() {
        foreach (var info in Hid.Enumerate()) {
            if (info.Vid == 0xB6A4 || info.Product.Contains("Phantom") || info.Product.Contains("Artemis")) {
                Console.WriteLine(string.Format("FOUND: VID=0x{0:X4} PID=0x{1:X4} UP=0x{2:X2} U=0x{3:X2} Path='{4}' Prod='{5}'", info.Vid, info.Pid, info.UsagePage, info.Usage, info.Path, info.Product));
                IntPtr h = CreateFile(info.Path, 0xC0000000 /* GENERIC_READ | GENERIC_WRITE */, 3 /* FILE_SHARE_READ | FILE_SHARE_WRITE */, IntPtr.Zero, 3 /* OPEN_EXISTING */, 0, IntPtr.Zero);
                if (h.ToInt64() <= 0) {
                    h = CreateFile(info.Path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                }
                if (h.ToInt64() > 0) {
                    IntPtr pp;
                    if (HidD_GetPreparsedData(h, out pp)) {
                        HIDP_CAPS caps;
                        HidP_GetCaps(pp, out caps);
                        Console.WriteLine(string.Format("   Caps: UP=0x{0:X2} U=0x{1:X2} InLen={2} OutLen={3} FeatLen={4}", caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength));
                        HidD_FreePreparsedData(pp);
                    }
                    CloseHandle(h);
                } else {
                    Console.WriteLine("   Could not open handle (error: " + Marshal.GetLastWin32Error() + ")");
                }
            }
        }
    }
}
