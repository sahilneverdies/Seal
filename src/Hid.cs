

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Seal {


    public static class Hid {
        [StructLayout(LayoutKind.Sequential)]
        struct Attributes { public int Size; public ushort VendorId, ProductId, Version; }

        [StructLayout(LayoutKind.Sequential)]
        struct Caps {
            public ushort Usage, UsagePage;
            public ushort InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort LinkCollectionNodes;
            public ushort InputButtonCaps, InputValueCaps, InputDataIndices;
            public ushort OutputButtonCaps, OutputValueCaps, OutputDataIndices;
            public ushort FeatureButtonCaps, FeatureValueCaps, FeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct InterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
        [DllImport("hid.dll")] static extern bool HidD_GetAttributes(IntPtr h, ref Attributes a);
        [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr pp);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr pp);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pp, ref Caps c);
        [DllImport("hid.dll")] static extern bool HidD_GetFeature(IntPtr h, byte[] buf, int len);
        [DllImport("hid.dll")] static extern bool HidD_SetFeature(IntPtr h, byte[] buf, int len);
        [DllImport("hid.dll", CharSet = CharSet.Unicode)] static extern bool HidD_GetProductString(IntPtr h, byte[] buf, int len);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr parent, int flags);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, int size, out int needed, IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateFile(string path, int access, int share, IntPtr sec, int disp, int flags, IntPtr template);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr h);

        const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
        const int GENERIC_READ = unchecked((int)0x80000000), GENERIC_WRITE = 0x40000000;
        const int FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
        internal static readonly IntPtr Invalid = new IntPtr(-1);


        internal sealed class Info {
            public string Path, Product;
            public ushort VendorId, ProductId, UsagePage, Usage;
            public int InputLen, OutputLen, FeatureLen;
            public override string ToString() {
                return "VID_" + VendorId.ToString("X4") + " PID_" + ProductId.ToString("X4") +
                    " usage " + UsagePage.ToString("X2") + "/" + Usage.ToString("X2") +
                    " reports in/out/feat " + InputLen + "/" + OutputLen + "/" + FeatureLen +
                    (string.IsNullOrEmpty(Product) ? "" : " \"" + Product + "\"");
            }
        }

        internal static List<Info> Enumerate() {
            var found = new List<Info>();
            Guid guid;
            HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == Invalid) return found;
            try {
                var data = new InterfaceData();
                data.Size = Marshal.SizeOf(typeof(InterfaceData));
                for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data); i++) {
                    string path = DetailPath(set, ref data);
                    if (path == null) continue;
                    var info = Describe(path);
                    if (info != null) found.Add(info);
                }
            } catch (Exception ex) { Log.Write("hid enumerate: " + ex.Message); }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return found;
        }

        static string DetailPath(IntPtr set, ref InterfaceData data) {
            int needed;
            SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
            if (needed <= 0) return null;
            IntPtr buf = Marshal.AllocHGlobal(needed);
            try {


                Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize);
                if (!SetupDiGetDeviceInterfaceDetail(set, ref data, buf, needed, out needed, IntPtr.Zero)) return null;
                return Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
            } finally { Marshal.FreeHGlobal(buf); }
        }

        static Info Describe(string path) {
            IntPtr h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == Invalid) return null;
            IntPtr pp = IntPtr.Zero;
            try {
                var attrs = new Attributes();
                attrs.Size = Marshal.SizeOf(typeof(Attributes));
                if (!HidD_GetAttributes(h, ref attrs)) return null;
                if (!HidD_GetPreparsedData(h, out pp)) return null;
                var caps = new Caps();
                if (HidP_GetCaps(pp, ref caps) != 0x110000) return null;
                var info = new Info {
                    Path = path, VendorId = attrs.VendorId, ProductId = attrs.ProductId,
                    UsagePage = caps.UsagePage, Usage = caps.Usage,
                    InputLen = caps.InputReportByteLength, OutputLen = caps.OutputReportByteLength,
                    FeatureLen = caps.FeatureReportByteLength
                };
                var name = new byte[254];
                if (HidD_GetProductString(h, name, name.Length)) {
                    string s = System.Text.Encoding.Unicode.GetString(name);
                    int nul = s.IndexOf('\0');
                    info.Product = nul >= 0 ? s.Substring(0, nul) : s;
                }
                return info;
            } catch (Exception ex) { Log.Write("hid describe: " + ex.Message); return null; }
            finally { if (pp != IntPtr.Zero) HidD_FreePreparsedData(pp); CloseHandle(h); }
        }


        internal static IntPtr Open(string path) {
            return CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        }
        internal static bool GetFeature(IntPtr h, byte[] buf) { return HidD_GetFeature(h, buf, buf.Length); }
        internal static bool SetFeature(IntPtr h, byte[] buf) { return HidD_SetFeature(h, buf, buf.Length); }
    }

    public sealed class LampArray : IDisposable {
        public const ushort UsagePageLighting = 0x59, UsageLampArray = 0x01;
        const byte RepAttributes = 1, RepLampRequest = 2, RepLampResponse = 3, RepMultiUpdate = 4, RepRangeUpdate = 5, RepControl = 6;
        const byte FlagUpdateComplete = 0x01;
        const int MultiUpdateLamps = 8;
        public const uint KindKeyboard = 1;

        IntPtr handle = Hid.Invalid;
        readonly int featureLen;
        public readonly string Path, Product;
        public readonly ushort VendorId, ProductId;
        public readonly int LampCount;
        public readonly uint Kind, MinUpdateMicroseconds;
        public readonly int WidthMicrometres, HeightMicrometres;

        public readonly ushort[] KeyUsage;

        public readonly int[] X, Y;
        readonly bool[] programmable;

        LampArray(IntPtr h, Hid.Info info, byte[] attrs) {
            handle = h;
            featureLen = info.FeatureLen;
            Path = info.Path;
            Product = info.Product;
            VendorId = info.VendorId;
            ProductId = info.ProductId;
            LampCount = U16(attrs, 1);
            WidthMicrometres = I32(attrs, 3);
            HeightMicrometres = I32(attrs, 7);
            Kind = U32(attrs, 15);
            MinUpdateMicroseconds = U32(attrs, 19);
            KeyUsage = new ushort[LampCount];
            X = new int[LampCount];
            Y = new int[LampCount];
            programmable = new bool[LampCount];
            ReadLamps();
        }

        static ushort U16(byte[] b, int i) { return (ushort)(b[i] | (b[i + 1] << 8)); }
        static uint U32(byte[] b, int i) { return (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24)); }
        static int I32(byte[] b, int i) { return unchecked((int)U32(b, i)); }


        public static List<LampArray> All() {
            var found = new List<LampArray>();
            foreach (var info in Hid.Enumerate()) {
                if (info.UsagePage != UsagePageLighting || info.Usage != UsageLampArray) continue;
                if (info.FeatureLen < 24) { Log.Write("lamparray: " + info + ": feature report too short, skipped"); continue; }
                IntPtr h = Hid.Open(info.Path);
                if (h == Hid.Invalid) { Log.Write("lamparray: " + info + ": cannot open"); continue; }
                try {
                    var attrs = new byte[info.FeatureLen];
                    attrs[0] = RepAttributes;
                    if (!Hid.GetFeature(h, attrs)) { Log.Write("lamparray: " + info + ": attributes report refused"); Hid.CloseHandle(h); continue; }
                    var la = new LampArray(h, info, attrs);
                    Log.Write("lamparray: " + info + " -> " + la.Describe);
                    found.Add(la);
                } catch (Exception ex) { Log.Write("lamparray: " + info + ": " + ex.Message); Hid.CloseHandle(h); }
            }
            return found;
        }

        public bool UsableAsPerKey { get { return Kind == KindKeyboard && LampCount >= 8 && AnyProgrammable && Internal; } }

        public bool Internal {
            get {
                if ((Path ?? "").IndexOf("HID_DEVICE_SYSTEM_VHF", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return VendorId == 0x0D62 || VendorId == 0x0461 || VendorId == 0x04F2 || VendorId == 0x048D;
            }
        }


        public static LampArray FindKeyboard() {
            LampArray best = null;
            foreach (var la in All()) {
                if (!la.UsableAsPerKey || (best != null && la.LampCount <= best.LampCount)) { la.Dispose(); continue; }
                if (best != null) best.Dispose();
                best = la;
            }
            return best;
        }

        public string Describe {
            get {
                return LampCount + " lamps, kind " + Kind + ", " + (WidthMicrometres / 1000) + "x" + (HeightMicrometres / 1000) +
                    " mm, min update " + (MinUpdateMicroseconds / 1000) + " ms";
            }
        }
        public bool AnyProgrammable { get { foreach (bool p in programmable) if (p) return true; return false; } }
        public bool Programmable(int lamp) { return lamp >= 0 && lamp < programmable.Length && programmable[lamp]; }

        void ReadLamps() {
            for (int i = 0; i < LampCount; i++) {
                try {
                    var req = new byte[featureLen];
                    req[0] = RepLampRequest;
                    req[1] = (byte)i;
                    req[2] = (byte)(i >> 8);
                    if (!Hid.SetFeature(handle, req)) continue;
                    var rep = new byte[featureLen];
                    rep[0] = RepLampResponse;
                    if (!Hid.GetFeature(handle, rep)) continue;


                    int id = U16(rep, 1);
                    if (id < 0 || id >= LampCount) id = i;
                    X[id] = I32(rep, 3);
                    Y[id] = I32(rep, 7);
                    programmable[id] = featureLen > 27 && rep[27] != 0;

                    KeyUsage[id] = featureLen > 28 ? rep[28] : (ushort)0;
                } catch { }
            }
        }


        public void TakeOver(bool ours) {
            try {
                var b = new byte[featureLen];
                b[0] = RepControl;
                b[1] = (byte)(ours ? 0 : 1);
                Hid.SetFeature(handle, b);
            } catch (Exception ex) { Log.Write("lamparray control: " + ex.Message); }
        }


        public void SetAll(Rgb c, int intensity) {
            try {
                var b = new byte[featureLen];
                b[0] = RepRangeUpdate;
                b[1] = FlagUpdateComplete;
                b[2] = 0;
                b[3] = 0;
                b[4] = (byte)((LampCount - 1) & 0xFF);
                b[5] = (byte)((LampCount - 1) >> 8);
                b[6] = c.R;
                b[7] = c.G;
                b[8] = c.B;
                b[9] = (byte)intensity;
                Hid.SetFeature(handle, b);
            } catch (Exception ex) { Log.Write("lamparray range update: " + ex.Message); }
        }

        public void SetLamps(int[] ids, Rgb[] colors, int intensity) {
            if (ids == null || colors == null) return;
            int n = Math.Min(ids.Length, colors.Length);
            try {
                for (int at = 0; at < n; at += MultiUpdateLamps) {
                    int count = Math.Min(MultiUpdateLamps, n - at);
                    var b = new byte[featureLen];
                    b[0] = RepMultiUpdate;
                    b[1] = (byte)count;
                    b[2] = (byte)(at + count >= n ? FlagUpdateComplete : 0);
                    for (int j = 0; j < count; j++) {
                        int id = ids[at + j];
                        b[3 + j * 2] = (byte)(id & 0xFF);
                        b[4 + j * 2] = (byte)(id >> 8);
                        int ch = 3 + MultiUpdateLamps * 2 + j * 4;
                        var c = colors[at + j];
                        b[ch] = c.R;
                        b[ch + 1] = c.G;
                        b[ch + 2] = c.B;
                        b[ch + 3] = (byte)intensity;
                    }
                    if (!Hid.SetFeature(handle, b)) break;
                }
            } catch (Exception ex) { Log.Write("lamparray multi update: " + ex.Message); }
        }

        public void Dispose() {
            if (handle == Hid.Invalid) return;
            Hid.CloseHandle(handle);
            handle = Hid.Invalid;
        }
    }

    public sealed class PerKeyLighting : ILighting {
        readonly LampArray lamps;
        readonly int[] ids;
        readonly Rgb[] shown;
        readonly int[] leader;
        int backlight = 0x80 | 100;
        bool held;

        public PerKeyLighting(LampArray la) {
            lamps = la;
            ids = new int[la.LampCount];
            shown = new Rgb[la.LampCount];

            for (int i = 0; i < la.LampCount; i++) { ids[i] = i; shown[i] = new Rgb(0, 0, 0); }
            leader = KeyboardLayouts.Followers(la);
            Hold();
        }

        void Hold() { if (held) return; lamps.TakeOver(true); held = true; }

        public void Release() { if (!held) return; lamps.TakeOver(false); held = false; }

        public LightKind Kind { get { return LightKind.PerKey; } }
        public bool Inert { get { return false; } }
        public int Zones { get { return lamps.LampCount; } }
        public string Describe { get { return lamps.LampCount + " keys"; } }
        public bool Numpad { get { return lamps.LampCount > 90; } }
        public LampArray Device { get { return lamps; } }

        public Rgb[] GetColors() { return (Rgb[])shown.Clone(); }

        public void SetColors(Rgb[] c) {
            if (c == null) return;
            for (int i = 0; i < shown.Length && i < c.Length; i++) shown[i] = c[i];
            Paint();
        }

        public int GetBacklight() { return backlight; }
        public void SetBacklight(bool on, int level) {
            backlight = Math.Max(0, Math.Min(100, level)) | (on ? 0x80 : 0);
            Paint();
        }

        void Paint() {
            Hold();
            if ((backlight & 0x80) == 0) { lamps.SetAll(new Rgb(0, 0, 0), 0); return; }


            var c = new Rgb[shown.Length];
            for (int i = 0; i < c.Length; i++) c[i] = shown[leader[i] >= 0 ? leader[i] : i];
            lamps.SetLamps(ids, c, (backlight & 0x7F) * 255 / 100);
        }
    }
}
