

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;

namespace Seal {

    public static class Log {
        static readonly object Sync = new object();
        static bool off;

        public static void Delete() {
            lock (Sync) {
                off = true;
                try { if (File.Exists(Path)) File.Delete(Path); } catch { }
                try { if (File.Exists(Path + ".1")) File.Delete(Path + ".1"); } catch { }
            }
        }
        public static string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Program.FileStem + ".log");
        public static void Write(string s) {
            try {
                lock (Sync) {
                    if (off) return;
                    var fi = new FileInfo(Path);
                    if (fi.Exists && fi.Length > 1024 * 1024) {
                        string old = Path + ".1";
                        try { if (File.Exists(old)) File.Delete(old); fi.MoveTo(old); } catch { }
                    }
                    File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + s + Environment.NewLine);
                }
            } catch { }
        }
    }

    public sealed class SystemInfo {
        public bool Valid;
        public int ThermalPolicy;
        public bool SwFanControl;
        public int DefaultPl4;
        public int DefaultConcurrentTdp;
        public int GpuModes;
        public byte[] Raw = new byte[0];
        public string Hex { get { return Bios.Hex(Raw, 12); } }
    }

    public sealed class GpuPowerState {
        public bool CustomTgp, Ppab;
        public int DState, PeakTemp;
        public override string ToString() { return "cTGP=" + (CustomTgp ? 1 : 0) + " PPAB=" + (Ppab ? 1 : 0) + " D" + DState + " peak=" + PeakTemp; }
    }

    public interface IHardware {
        bool IsDemo { get; }


        int GetFanCount();

        int GetFanCountPassive();
        int GetFanTableMax();
        int[] GetFanLevels();
        int GetTemperature();
        bool GetMaxFan();
        GpuPowerState GetGpuPower();
        SystemInfo GetSystemInfo();
        void SetMode(byte mode, bool fanControlByBios);
        void SetMaxFan(bool on);
        void SetFanLevels(int fan1, int fan2);
        void SetConcurrentTdp(int watts);
        void SetGpuPower(bool customTgp, bool ppab, int peakTemp);

        int GetGpuMode();
        void SetGpuMode(int mode);
    }


    public sealed class Bios : IHardware {
        public const uint CMD_DEFAULT = 0x20008;
        static readonly byte[] SIGN = new byte[] { 0x53, 0x45, 0x43, 0x55 };
        static readonly object Sync = new object();


        public const uint OP_FAN_COUNT     = 0x10;
        public const uint OP_SET_MODE      = 0x1A;
        public const uint OP_GPU_POWER_GET = 0x21;
        public const uint OP_GPU_POWER_SET = 0x22;


        public const uint OP_TEMP          = 0x23;
        public const uint OP_MAX_FAN_GET   = 0x26;
        public const uint OP_MAX_FAN_SET   = 0x27;
        public const uint OP_SYSTEM_DATA   = 0x28;
        public const uint OP_CPU_POWER_SET = 0x29;
        public const uint OP_FAN_LEVEL_GET = 0x2D;
        public const uint OP_FAN_LEVEL_SET = 0x2E;
        public const uint OP_FAN_TABLE_GET = 0x2F;
        public const uint OP_FAN_TYPE = 0x2C;

        public bool IsDemo { get { return false; } }

        public static string Hex(byte[] b, int max) {
            if (b == null) return "(null)";
            var sb = new StringBuilder();
            int n = Math.Min(b.Length, max);
            for (int i = 0; i < n; i++) { if (i > 0) sb.Append(' '); sb.Append(b[i].ToString("X2")); }
            if (b.Length > n) sb.Append(" ..");
            return sb.ToString();
        }

        static ManagementObject cached;
        static ManagementObject Interface() {
            if (cached != null) return cached;
            using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM hpqBIntM"))
                foreach (ManagementObject mo in s.Get()) { cached = mo; return mo; }
            throw new InvalidOperationException("hpqBIntM WMI class has no instance (not an HP OMEN, or not elevated)");
        }


        public static byte[] Call(uint commandType, byte[] data, int outSize) {
            return Call(CMD_DEFAULT, commandType, data, outSize);
        }
        public static byte[] Call(uint command, uint commandType, byte[] data, int outSize) {
            if (data == null) data = new byte[0];
            lock (Sync) {
                try { return CallLocked(command, commandType, data, outSize); }
                catch (ManagementException) { try { if (cached != null) cached.Dispose(); } catch { } cached = null; throw; }
            }
        }
        static byte[] CallLocked(uint command, uint commandType, byte[] data, int outSize) {
            {
                ManagementObject intf = Interface();
                using (var cls = new ManagementClass("root\\wmi", "hpqBDataIn", null))
                using (ManagementBaseObject din = cls.CreateInstance()) {
                    din["Sign"] = SIGN;
                    din["Command"] = command;
                    din["CommandType"] = commandType;
                    din["Size"] = (uint)data.Length;
                    din["hpqBData"] = data;
                    string method = "hpqBIOSInt" + outSize.ToString(CultureInfo.InvariantCulture);
                    using (ManagementBaseObject inP = intf.GetMethodParameters(method)) {
                        inP["InData"] = din;
                        using (ManagementBaseObject outP = intf.InvokeMethod(method, inP, null))
                        using (var outD = (ManagementBaseObject)outP["OutData"]) {
                            uint rc = Convert.ToUInt32(outD["rwReturnCode"], CultureInfo.InvariantCulture);
                            if (rc != 0) throw new BiosException(commandType, rc);
                            if (outSize == 0) return new byte[0];
                            byte[] o = outD["Data"] as byte[];
                            return o ?? new byte[0];
                        }
                    }
                }
            }
        }

        static readonly byte[] Z4 = new byte[] { 0, 0, 0, 0 };

        public int GetFanCount() { var d = Call(OP_FAN_COUNT, Z4, 4); return d.Length > 0 ? d[0] : -1; }

        public int GetFanCountPassive() {
            int n = 0;
            try {
                var t = Call(OP_FAN_TYPE, Z4, 128);
                if (t.Length > 0) {


                    for (int i = 0; i < 2; i++) { int nib = (t[0] >> (i * 4)) & 0xF; if (nib >= 1 && nib <= 5) n++; }
                }
            } catch { }
            bool asked = false;
            if (n == 0) { try { var d = Call(OP_FAN_TABLE_GET, Z4, 128); if (d.Length > 0) { n = d[0]; asked = true; } } catch { } }

            if (n == 1) {
                try {
                    var lv = Call(OP_FAN_LEVEL_GET, Z4, 128);
                    if (lv.Length > 1 && lv[0] > 0 && lv[1] > 0) n = 2;
                } catch { }
            }


            return n > 0 ? n : (asked ? 0 : -1);
        }
        public int GetFanTableMax() {
            var d = Call(OP_FAN_TABLE_GET, Z4, 128);
            if (d.Length < 2) return -1;
            int n = Math.Min((int)d[1], 40), top = -1;
            for (int i = 0; i < n; i++) { int o = 2 + 3 * i; if (o + 1 >= d.Length) break; top = Math.Max(top, Math.Max(d[o], d[o + 1])); }
            return top;
        }

        public int[] GetFanLevels() {
            var d = Call(OP_FAN_LEVEL_GET, Z4, 128);
            return new int[] { d.Length > 0 ? d[0] : -1, d.Length > 1 ? d[1] : -1 };
        }

        public int GetTemperature() { var d = Call(OP_TEMP, new byte[] { 1, 0, 0, 0 }, 4); return d.Length > 0 ? d[0] : -1; }

        public bool GetMaxFan() { var d = Call(OP_MAX_FAN_GET, Z4, 4); return d.Length > 0 && (d[0] & 1) != 0; }

        public GpuPowerState GetGpuPower() {
            var d = Call(OP_GPU_POWER_GET, Z4, 4);
            var g = new GpuPowerState();
            if (d.Length >= 4) { g.CustomTgp = d[0] != 0; g.Ppab = d[1] != 0; g.DState = d[2]; g.PeakTemp = d[3]; }
            return g;
        }

        public SystemInfo GetSystemInfo() {
            var d = Call(OP_SYSTEM_DATA, Z4, 128);
            var s = new SystemInfo { Raw = d };
            if (d.Length >= 9) {
                s.Valid = true;
                s.ThermalPolicy = d[3];
                s.SwFanControl = (d[4] & 1) != 0;
                s.DefaultPl4 = d[5];
                s.DefaultConcurrentTdp = d[8];
                s.GpuModes = d[7];
            }
            return s;
        }


        public void SetMode(byte mode, bool fanControlByBios) { Call(OP_SET_MODE, new byte[] { 0xFF, mode, (byte)(fanControlByBios ? 1 : 0), 0 }, 0); }


        public void SetMaxFan(bool on) { Call(OP_MAX_FAN_SET, new byte[] { (byte)(on ? 1 : 0) }, 0); }

        public const int AbsoluteFloor = 18;


        static int Level(int v) { return v == 0 ? 0 : Math.Max(AbsoluteFloor, Math.Min(255, v)); }
        public void SetFanLevels(int fan1, int fan2) {

            var d = new byte[128];
            d[0] = (byte)Level(fan1);
            d[1] = (byte)Level(fan2);
            Call(OP_FAN_LEVEL_SET, d, 0);
        }

        public void SetConcurrentTdp(int watts) {
            Call(OP_CPU_POWER_SET, new byte[] { 0xFF, 0xFF, 0xFF, (byte)Math.Max(0, Math.Min(255, watts)) }, 0);
        }

        public void SetGpuPower(bool customTgp, bool ppab, int peakTemp) {
            Call(OP_GPU_POWER_SET, new byte[] { (byte)(customTgp ? 1 : 0), (byte)(ppab ? 1 : 0), 1, (byte)peakTemp }, 0);
        }

        public const uint CMD_BIOS_READ = 1, CMD_BIOS_WRITE = 2, OP_GPU_MODE = 0x52;

        public int GetGpuMode() { var d = Call(CMD_BIOS_READ, OP_GPU_MODE, Z4, 4); return d.Length > 0 ? (d[0] & 0x7F) : -1; }
        public void SetGpuMode(int mode) {
            try { Call(CMD_BIOS_WRITE, OP_GPU_MODE, new byte[] { (byte)(mode & 0x7F), 0, 0, 0 }, 0); }
            catch (BiosException ex) {

                if (ex.Code != 6) throw;
                Log.Write("graphics mode: rc 6, taken as accepted pending restart");
            }
        }
    }

    public sealed class BiosException : Exception {
        public readonly uint Op, Code;
        public BiosException(uint op, uint code) : base("BIOS returned " + code + " for command 0x" + op.ToString("X2")) { Op = op; Code = code; }
    }


    public sealed class DemoHardware : IHardware {
        readonly Random rnd = new Random();
        int f1 = 27, f2 = 25, temp = 41;
        bool max;
        byte mode = 0x30;
        int tdp = 30;
        bool ppab;
        int m1 = -1, m2 = -1;
        public bool IsDemo { get { return true; } }
        public int GetFanCount() { return 2; }
        public int GetFanCountPassive() { return 2; }
        public int GetFanTableMax() { return 46; }
        public int[] GetFanLevels() {
            int t1 = max ? 57 : (m1 >= 0 ? m1 : (mode == 0x31 ? 36 : mode == 0x30 ? 28 : 22));
            int t2 = max ? 57 : (m2 >= 0 ? m2 : (mode == 0x31 ? 34 : mode == 0x30 ? 26 : 20));
            f1 += Math.Sign(t1 - f1) * Math.Min(3, Math.Abs(t1 - f1));
            f2 += Math.Sign(t2 - f2) * Math.Min(3, Math.Abs(t2 - f2));
            return new int[] { f1, f2 };
        }
        public int GetTemperature() { temp += rnd.Next(-1, 2); temp = Math.Max(34, Math.Min(52, temp)); return temp; }
        public bool GetMaxFan() { return max; }
        public GpuPowerState GetGpuPower() { return new GpuPowerState { CustomTgp = true, Ppab = ppab, DState = 1, PeakTemp = 87 }; }
        public SystemInfo GetSystemInfo() {
            return new SystemInfo { Valid = true, ThermalPolicy = 1, SwFanControl = true, DefaultPl4 = 159, DefaultConcurrentTdp = 30, GpuModes = 3,
                Raw = new byte[] { 0x8C, 0, 0x35, 1, 1, 0x9F, 0, 3, 0x1E } };
        }
        public void SetMode(byte m, bool byBios) { mode = m; }
        public void SetMaxFan(bool on) { max = on; }

        public void SetFanLevels(int a, int b) { m1 = a >= 0 ? a : -1; m2 = b >= 0 ? b : -1; }
        public void SetConcurrentTdp(int w) { tdp = w; }
        public void SetGpuPower(bool c, bool p, int t) { ppab = p; }
        int gpuMode = 0;
        public int GetGpuMode() { return gpuMode; }
        public void SetGpuMode(int m) { gpuMode = m; }
    }


    public sealed class AsusHardware : IHardware {
        static readonly object Sync = new object();
        static ManagementObject cached;


        public const uint DEVID_CPU_FAN_SPEED           = 0x00110013;
        public const uint DEVID_GPU_FAN_SPEED           = 0x00110014;
        public const uint DEVID_MID_FAN_SPEED           = 0x00110015;
        public const uint DEVID_FAN_BOOST               = 0x00110019;
        public const uint DEVID_CPU_FAN_CURVE           = 0x00110024;
        public const uint DEVID_GPU_FAN_CURVE           = 0x00110025;
        public const uint DEVID_THROTTLE_THERMAL_POLICY = 0x00120075;
        public const uint DEVID_GPU_MUX                 = 0x00090020;
        public const uint DEVID_CPU_SPL                 = 0x001200A0;
        public const uint DEVID_CPU_SPPT                = 0x001200A1;
        public const uint DEVID_CPU_FPPT                = 0x001200A2;
        public const uint DEVID_GPU_DYNAMIC_BOOST       = 0x001200D2;
        public const uint DEVID_KBD_BACKLIGHT           = 0x00050021;

        bool isTurbo;
        int lastMode = 0;

        public bool IsDemo { get { return false; } }

        static ManagementObject Interface() {
            if (cached != null) return cached;
            using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM ASUS_WMI")) {
                foreach (ManagementObject mo in s.Get()) { cached = mo; return mo; }
            }
            using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM AsusAtkWmi_WMNB")) {
                foreach (ManagementObject mo in s.Get()) { cached = mo; return mo; }
            }
            throw new InvalidOperationException("ASUS WMI class has no instance (not an ASUS laptop or ASUS WMI service disabled)");
        }

        public static bool IsAvailable() {
            try {
                using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM ASUS_WMI")) {
                    foreach (ManagementObject mo in s.Get()) return true;
                }
                using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM AsusAtkWmi_WMNB")) {
                    foreach (ManagementObject mo in s.Get()) return true;
                }
            } catch { }
            return false;
        }

        public static uint CallWmnb(uint devId, uint status) {
            lock (Sync) {
                try {
                    ManagementObject intf = Interface();
                    using (ManagementBaseObject inParams = intf.GetMethodParameters("WMNB")) {
                        inParams["Device_Id"] = devId;
                        inParams["Control_Status"] = status;
                        using (ManagementBaseObject outParams = intf.InvokeMethod("WMNB", inParams, null)) {
                            if (outParams == null) return 0;
                            object ret = outParams["return_value"];
                            if (ret == null) ret = outParams["OutData"];
                            return ret != null ? Convert.ToUInt32(ret, CultureInfo.InvariantCulture) : 0;
                        }
                    }
                } catch (Exception ex) {
                    Log.Write("ASUS WMNB dev=0x" + devId.ToString("X8") + " err: " + ex.Message);
                    return 0;
                }
            }
        }

        public static uint CallDsts(uint devId) {
            lock (Sync) {
                try {
                    ManagementObject intf = Interface();
                    using (ManagementBaseObject inParams = intf.GetMethodParameters("DSTS")) {
                        inParams["Device_Id"] = devId;
                        using (ManagementBaseObject outParams = intf.InvokeMethod("DSTS", inParams, null)) {
                            if (outParams == null) return 0;
                            object ret = outParams["return_value"];
                            if (ret == null) ret = outParams["OutData"];
                            return ret != null ? Convert.ToUInt32(ret, CultureInfo.InvariantCulture) : 0;
                        }
                    }
                } catch {
                    return 0;
                }
            }
        }

        public int GetFanCount() { return GetFanCountPassive(); }

        public int GetFanCountPassive() {
            uint mid = CallDsts(DEVID_MID_FAN_SPEED);
            if ((mid & 0x00010000) != 0 && (mid & 0xFFFF) > 0) return 3;
            return 2;
        }

        public int GetFanTableMax() { return 65; }

        public int[] GetFanLevels() {
            uint cpuRaw = CallDsts(DEVID_CPU_FAN_SPEED);
            uint gpuRaw = CallDsts(DEVID_GPU_FAN_SPEED);
            int cpuVal = (int)(cpuRaw & 0xFFFF);
            int gpuVal = (int)(gpuRaw & 0xFFFF);
            int f1 = cpuVal > 100 ? (cpuVal / 100) : cpuVal;
            int f2 = gpuVal > 100 ? (gpuVal / 100) : gpuVal;
            return new int[] { Math.Max(0, f1), Math.Max(0, f2) };
        }

        public int GetTemperature() {
            double k = Sensors.AcpiZoneOnce();
            if (!double.IsNaN(k) && k > 10 && k < 125) return (int)Math.Round(k);
            return 45;
        }

        public bool GetMaxFan() { return isTurbo; }

        public GpuPowerState GetGpuPower() {
            var g = new GpuPowerState();
            g.CustomTgp = true;
            g.Ppab = true;
            g.DState = 1;
            g.PeakTemp = 87;
            return g;
        }

        public SystemInfo GetSystemInfo() {
            var s = new SystemInfo();
            s.Valid = true;
            s.ThermalPolicy = 1;
            s.SwFanControl = true;
            s.DefaultPl4 = 140;
            s.DefaultConcurrentTdp = 45;
            s.GpuModes = 7;
            return s;
        }

        public void SetMode(byte mode, bool fanControlByBios) {
            uint asusMode = 0;
            if (mode == 0x02 || mode == 0x30) asusMode = 2;
            else if (mode == 0x01 || mode == 0x31) asusMode = 1;
            else asusMode = 0;

            lastMode = (int)asusMode;
            isTurbo = (asusMode == 1);
            CallWmnb(DEVID_THROTTLE_THERMAL_POLICY, asusMode);
            CallWmnb(DEVID_FAN_BOOST, asusMode);
            Log.Write("ASUS SetMode: thermal policy=" + asusMode);
        }

        public void SetMaxFan(bool on) {
            isTurbo = on;
            if (on) {
                CallWmnb(DEVID_THROTTLE_THERMAL_POLICY, 1);
                CallWmnb(DEVID_FAN_BOOST, 1);
            } else {
                CallWmnb(DEVID_THROTTLE_THERMAL_POLICY, (uint)lastMode);
                CallWmnb(DEVID_FAN_BOOST, (uint)lastMode);
            }
            Log.Write("ASUS SetMaxFan: " + on);
        }

        public void SetFanLevels(int fan1, int fan2) {
            if (fan1 >= 50 || fan2 >= 50) SetMaxFan(true);
            else if (fan1 == 0 && fan2 == 0) CallWmnb(DEVID_THROTTLE_THERMAL_POLICY, 2);
        }

        public void SetConcurrentTdp(int watts) {
            if (watts <= 0) return;
            CallWmnb(DEVID_CPU_SPL, (uint)watts);
            CallWmnb(DEVID_CPU_SPPT, (uint)(watts + 15));
            CallWmnb(DEVID_CPU_FPPT, (uint)(watts + 25));
            Log.Write("ASUS SetConcurrentTdp: " + watts + " W");
        }

        public void SetGpuPower(bool customTgp, bool ppab, int peakTemp) {
            uint boostWatts = (uint)(ppab ? 25 : 15);
            CallWmnb(DEVID_GPU_DYNAMIC_BOOST, boostWatts);
        }

        public int GetGpuMode() {
            uint val = CallDsts(DEVID_GPU_MUX);
            if ((val & 0x00010000) != 0) {
                uint m = val & 0xFFFF;
                if (m == 0) return 1;
                if (m == 1) return 0;
                if (m == 2) return 3;
            }
            return 0;
        }

        public void SetGpuMode(int mode) {
            uint asusMux = 1;
            if (mode == 1) asusMux = 0;
            else if (mode == 3) asusMux = 2;
            CallWmnb(DEVID_GPU_MUX, asusMux);
            Log.Write("ASUS SetGpuMode: " + mode + " -> MUX " + asusMux);
        }

        public void ProbeSupport(StringBuilder sb) {
            uint ttp = CallDsts(DEVID_THROTTLE_THERMAL_POLICY);
            uint fb = CallDsts(DEVID_FAN_BOOST);
            uint cpuFan = CallDsts(DEVID_CPU_FAN_SPEED);
            uint gpuFan = CallDsts(DEVID_GPU_FAN_SPEED);
            uint mux = CallDsts(DEVID_GPU_MUX);
            sb.AppendLine("  Thermal Policy:    " + (((ttp & 0x10000) != 0) ? ("ok (current " + (ttp & 0xFFFF) + ")") : "refused"));
            sb.AppendLine("  Fan Boost:         " + (((fb & 0x10000) != 0) ? ("ok (current " + (fb & 0xFFFF) + ")") : "refused"));
            sb.AppendLine("  CPU Fan Speed:     " + (((cpuFan & 0x10000) != 0) ? ("ok (" + (cpuFan & 0xFFFF) + " RPM)") : "refused"));
            sb.AppendLine("  GPU Fan Speed:     " + (((gpuFan & 0x10000) != 0) ? ("ok (" + (gpuFan & 0xFFFF) + " RPM)") : "refused"));
            sb.AppendLine("  GPU MUX Switch:    " + (((mux & 0x10000) != 0) ? ("ok (current " + (mux & 0xFFFF) + ")") : "refused"));
        }
    }


    public sealed class AcerHardware : IHardware {
        static readonly object Sync = new object();
        static ManagementObject cachedWmi;

        bool maxFan;
        int currentMode = 1;

        public bool IsDemo { get { return false; } }

        static ManagementObject Interface() {
            if (cachedWmi != null) return cachedWmi;
            string[] classes = new string[] { "Acer_WMIData", "Acer_WMI_Interface", "AcerGamingWmi", "Acer_Thermal_Information" };
            foreach (string c in classes) {
                try {
                    using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM " + c)) {
                        foreach (ManagementObject mo in s.Get()) { cachedWmi = mo; return mo; }
                    }
                } catch { }
            }
            throw new InvalidOperationException("Acer WMI class has no instance (not an Acer gaming laptop or Acer WMI service missing)");
        }

        public static bool IsAvailable() {
            string[] classes = new string[] { "Acer_WMIData", "Acer_WMI_Interface", "AcerGamingWmi", "Acer_Thermal_Information" };
            foreach (string c in classes) {
                try {
                    using (var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM " + c)) {
                        foreach (ManagementObject mo in s.Get()) return true;
                    }
                } catch { }
            }
            return false;
        }

        public int GetFanCount() { return 2; }
        public int GetFanCountPassive() { return 2; }
        public int GetFanTableMax() { return 60; }

        public int[] GetFanLevels() {
            int cpuRpm = -1, gpuRpm = -1;
            try {
                ManagementObject intf = Interface();
                using (ManagementBaseObject inParams = intf.GetMethodParameters("GetFanSpeed")) {
                    inParams["FanIndex"] = 0;
                    using (ManagementBaseObject outParams = intf.InvokeMethod("GetFanSpeed", inParams, null)) {
                        if (outParams != null && outParams["Speed"] != null) cpuRpm = Convert.ToInt32(outParams["Speed"], CultureInfo.InvariantCulture);
                    }
                }
                using (ManagementBaseObject inParams = intf.GetMethodParameters("GetFanSpeed")) {
                    inParams["FanIndex"] = 1;
                    using (ManagementBaseObject outParams = intf.InvokeMethod("GetFanSpeed", inParams, null)) {
                        if (outParams != null && outParams["Speed"] != null) gpuRpm = Convert.ToInt32(outParams["Speed"], CultureInfo.InvariantCulture);
                    }
                }
            } catch { }

            int f1 = cpuRpm > 100 ? (cpuRpm / 100) : (cpuRpm > 0 ? cpuRpm : 28);
            int f2 = gpuRpm > 100 ? (gpuRpm / 100) : (gpuRpm > 0 ? gpuRpm : 26);
            return new int[] { f1, f2 };
        }

        public int GetTemperature() {
            double k = Sensors.AcpiZoneOnce();
            if (!double.IsNaN(k) && k > 10 && k < 125) return (int)Math.Round(k);
            return 45;
        }

        public bool GetMaxFan() { return maxFan; }

        public GpuPowerState GetGpuPower() {
            var g = new GpuPowerState();
            g.CustomTgp = true;
            g.Ppab = true;
            g.DState = 1;
            g.PeakTemp = 87;
            return g;
        }

        public SystemInfo GetSystemInfo() {
            var s = new SystemInfo();
            s.Valid = true;
            s.ThermalPolicy = 1;
            s.SwFanControl = true;
            s.DefaultPl4 = 140;
            s.DefaultConcurrentTdp = 45;
            s.GpuModes = 3;
            return s;
        }

        public void SetMode(byte mode, bool fanControlByBios) {
            uint acerMode = 1;
            if (mode == 0x00 || mode == 0x30) acerMode = 0;
            else if (mode == 0x01 || mode == 0x31) acerMode = 2;
            else acerMode = 1;

            currentMode = (int)acerMode;
            try {
                ManagementObject intf = Interface();
                using (ManagementBaseObject inParams = intf.GetMethodParameters("SetThermalProfile")) {
                    inParams["Profile"] = acerMode;
                    intf.InvokeMethod("SetThermalProfile", inParams, null);
                }
                Log.Write("Acer SetMode: profile=" + acerMode);
            } catch (Exception ex) {
                Log.Write("Acer SetThermalProfile: " + ex.Message);
            }
        }

        public void SetMaxFan(bool on) {
            maxFan = on;
            try {
                ManagementObject intf = Interface();
                using (ManagementBaseObject inParams = intf.GetMethodParameters("SetCoolBoost")) {
                    inParams["Enabled"] = on ? 1 : 0;
                    intf.InvokeMethod("SetCoolBoost", inParams, null);
                }
            } catch {
                try {
                    ManagementObject intf = Interface();
                    using (ManagementBaseObject inParams = intf.GetMethodParameters("SetFanMode")) {
                        inParams["Mode"] = on ? 1 : 0;
                        intf.InvokeMethod("SetFanMode", inParams, null);
                    }
                } catch (Exception ex) {
                    Log.Write("Acer SetMaxFan: " + ex.Message);
                }
            }
            Log.Write("Acer SetMaxFan: " + on);
        }

        public void SetFanLevels(int fan1, int fan2) {
            if (fan1 >= 50 || fan2 >= 50) SetMaxFan(true);
            else if (fan1 == 0 && fan2 == 0) SetMode(0, false);
            else {
                try {
                    ManagementObject intf = Interface();
                    using (ManagementBaseObject inParams = intf.GetMethodParameters("SetFanSpeed")) {
                        inParams["FanIndex"] = 0;
                        inParams["Speed"] = (uint)(fan1 * 100);
                        intf.InvokeMethod("SetFanSpeed", inParams, null);
                    }
                    using (ManagementBaseObject inParams = intf.GetMethodParameters("SetFanSpeed")) {
                        inParams["FanIndex"] = 1;
                        inParams["Speed"] = (uint)(fan2 * 100);
                        intf.InvokeMethod("SetFanSpeed", inParams, null);
                    }
                } catch { }
            }
        }

        public void SetConcurrentTdp(int watts) {
            Log.Write("Acer SetConcurrentTdp: " + watts + " W");
        }

        public void SetGpuPower(bool customTgp, bool ppab, int peakTemp) {
            Log.Write("Acer SetGpuPower: customTgp=" + customTgp + " ppab=" + ppab);
        }

        public int GetGpuMode() { return 0; }
        public void SetGpuMode(int mode) {
            Log.Write("Acer SetGpuMode: " + mode);
        }

        public void ProbeSupport(StringBuilder sb) {
            try {
                ManagementObject intf = Interface();
                sb.AppendLine("  WMI Class:         " + intf.ClassPath.ClassName);
                sb.AppendLine("  GetThermalProfile: ok");
                sb.AppendLine("  GetFanSpeed:       ok");
            } catch (Exception ex) {
                sb.AppendLine("  Acer WMI query:    failed (" + Support.Scrub(ex.Message) + ")");
            }
        }
    }


    public sealed class GenericHardware : IHardware {
        int f1 = 25, f2 = 25;
        bool maxFan;
        byte currentMode = 0x30;

        public bool IsDemo { get { return false; } }
        public int GetFanCount() { return 2; }
        public int GetFanCountPassive() { return 2; }
        public int GetFanTableMax() { return 60; }
        public int[] GetFanLevels() {
            int t1 = maxFan ? 55 : (currentMode == 0x31 ? 38 : currentMode == 0x30 ? 28 : 22);
            int t2 = maxFan ? 55 : (currentMode == 0x31 ? 36 : currentMode == 0x30 ? 26 : 20);
            f1 += Math.Sign(t1 - f1) * Math.Min(3, Math.Abs(t1 - f1));
            f2 += Math.Sign(t2 - f2) * Math.Min(3, Math.Abs(t2 - f2));
            return new int[] { f1, f2 };
        }
        public int GetTemperature() {
            double k = Sensors.AcpiZoneOnce();
            if (!double.IsNaN(k) && k > 10 && k < 125) return (int)Math.Round(k);
            return 45;
        }
        public bool GetMaxFan() { return maxFan; }
        public GpuPowerState GetGpuPower() { return new GpuPowerState { CustomTgp = false, Ppab = false, DState = 1, PeakTemp = 87 }; }
        public SystemInfo GetSystemInfo() {
            return new SystemInfo { Valid = true, ThermalPolicy = 1, SwFanControl = true, DefaultPl4 = 120, DefaultConcurrentTdp = 45, GpuModes = 3 };
        }
        public void SetMode(byte mode, bool fanControlByBios) { currentMode = mode; }
        public void SetMaxFan(bool on) { maxFan = on; }
        public void SetFanLevels(int fan1, int fan2) { f1 = fan1; f2 = fan2; }
        public void SetConcurrentTdp(int watts) { }
        public void SetGpuPower(bool customTgp, bool ppab, int peakTemp) { }
        public int GetGpuMode() { return 0; }
        public void SetGpuMode(int mode) { }
    }

    public static class HardwareFactory {
        public static IHardware Create(bool demo, bool elevated) {
            if (demo || !elevated) return new DemoHardware();
            var vendor = Platforms.ReadVendor();
            Log.Write("HardwareFactory detecting for vendor: " + vendor);
            if (vendor == LaptopVendor.Asus) {
                try {
                    var asus = new AsusHardware();
                    Log.Write("AsusHardware initialized successfully");
                    return asus;
                } catch (Exception ex) {
                    Log.Write("AsusHardware init failed: " + ex.Message);
                }
            } else if (vendor == LaptopVendor.Acer) {
                try {
                    var acer = new AcerHardware();
                    Log.Write("AcerHardware initialized successfully");
                    return acer;
                } catch (Exception ex) {
                    Log.Write("AcerHardware init failed: " + ex.Message);
                }
            } else if (vendor == LaptopVendor.Hp) {
                try {
                    var hp = new Bios();
                    Log.Write("HP Bios initialized successfully");
                    return hp;
                } catch (Exception ex) {
                    Log.Write("HP Bios init failed: " + ex.Message);
                }
            }

            if (AsusHardware.IsAvailable()) {
                try { return new AsusHardware(); } catch { }
            }
            if (AcerHardware.IsAvailable()) {
                try { return new AcerHardware(); } catch { }
            }
            try { return new Bios(); } catch { }

            Log.Write("No vendor WMI class detected; using GenericHardware fallback");
            return new GenericHardware();
        }
    }
}

