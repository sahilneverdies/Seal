

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace Seal {


    public sealed class CpuTelemetry {
        public double DieTemp = double.NaN;
        public int TjMax;
        public double Pl1 = double.NaN, Pl2 = double.NaN;
        public bool Pl1On, Pl2On, PlLocked;
        public double Watts = double.NaN;
        public double Mhz = double.NaN;
        public string Throttle = "";
    }


    public abstract class CpuRegisters : IDisposable {
        readonly object sync = new object();
        protected readonly PawnIoModule Module;
        ulong lastEnergy;
        DateTime lastEnergyAt = DateTime.MinValue;
        protected double EnergyUnit;

        protected CpuRegisters(PawnIoModule module) { Module = module; }

        public abstract string Describe { get; }
        protected abstract CpuTelemetry PollCore();

        public CpuTelemetry Poll() { return Poll(true); }

        public CpuTelemetry Poll(bool full) {
            lock (sync) {
                light = !full;
                try { return PollCore(); } finally { light = false; }
            }
        }
        bool light;

        public virtual void Dispose() { if (Module != null) Module.Dispose(); }

        const uint IA32_MPERF = 0xE7, IA32_APERF = 0xE8;
        ulong lastAperf, lastMperf;
        bool haveClock;
        protected double BaseMhz;

        [DllImport("kernel32.dll")] static extern IntPtr SetThreadAffinityMask(IntPtr thread, IntPtr mask);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();

        protected double Clock() {
            if (BaseMhz <= 0 || light) return double.NaN;
            ulong a = 0, m = 0;
            bool ok;
            IntPtr prev = IntPtr.Zero;
            Thread.BeginThreadAffinity();
            try {
                prev = SetThreadAffinityMask(GetCurrentThread(), (IntPtr)1);


                ok = prev != IntPtr.Zero && Msr(IA32_APERF, out a) && Msr(IA32_MPERF, out m);
            } finally {
                if (prev != IntPtr.Zero) SetThreadAffinityMask(GetCurrentThread(), prev);
                Thread.EndThreadAffinity();
            }
            if (!ok) return double.NaN;
            double mhz = double.NaN;
            if (haveClock) {
                ulong da = a - lastAperf, dm = m - lastMperf;


                if (dm > 0 && da > 0 && da < dm * 12) mhz = BaseMhz * da / (double)dm;
            }
            lastAperf = a; lastMperf = m; haveClock = true;
            return mhz > 200 && mhz < 12000 ? mhz : double.NaN;
        }

        protected static double NominalMhz() {
            try {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    if (k != null) { object v = k.GetValue("~MHz"); if (v is int) return (int)v; }
            } catch { }
            return 0;
        }

        protected bool Msr(uint msr, out ulong v) {
            var o = new ulong[1];
            bool ok = Module.Call("ioctl_read_msr", new ulong[] { msr }, o);
            v = ok ? o[0] : 0;
            return ok;
        }


        protected double Power(ulong now) {
            if (light) return double.NaN;
            DateTime t = DateTime.UtcNow;
            double w = double.NaN;
            if (lastEnergyAt != DateTime.MinValue) {
                double secs = (t - lastEnergyAt).TotalSeconds;
                ulong delta = (now - lastEnergy) & 0xFFFFFFFFu;
                if (secs > 0.2 && secs < 120) w = delta * EnergyUnit / secs;
            }
            lastEnergy = now;
            lastEnergyAt = t;
            return w;
        }


        public static string Vendor() {
            try {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    if (k != null) return (k.GetValue("VendorIdentifier") as string ?? "").Trim();
            } catch { }
            return "";
        }

        static int isIntel;
        public static bool IsIntel {
            get {
                if (isIntel == 0) isIntel = Vendor().IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : -1;
                return isIntel == 1;
            }
        }


        public static CpuRegisters Open(out string why) { bool absent; return Open(out why, out absent); }
        public static CpuRegisters Open(out string why, out bool deviceAbsent) {
            string vendor = Vendor();
            deviceAbsent = false;
            if (IsIntel) {
                PawnIoModule m = PawnIo.Open("IntelMSR", out why, out deviceAbsent);
                return m == null ? null : new IntelCpu(m);
            }
            if (vendor.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0) {
                PawnIoModule m = PawnIo.Open("AMDFamily17", out why, out deviceAbsent);
                return m == null ? null : new AmdCpu(m);
            }
            why = "unknown CPU vendor '" + vendor + "'";
            return null;
        }
    }

    sealed class IntelCpu : CpuRegisters {
        const uint IA32_THERM_STATUS = 0x19C, IA32_TEMPERATURE_TARGET = 0x1A2, IA32_PACKAGE_THERM_STATUS = 0x1B1;
        const uint MSR_RAPL_POWER_UNIT = 0x606, MSR_PKG_POWER_LIMIT = 0x610, MSR_PKG_ENERGY_STATUS = 0x611;
        const uint MSR_PLATFORM_INFO = 0xCE;
        int tjMax;
        bool baseTried;
        double powerUnit;
        public IntelCpu(PawnIoModule module) : base(module) { }
        public override string Describe { get { return "Intel MSR" + (tjMax > 0 ? " · TjMax " + tjMax : ""); } }

        protected override CpuTelemetry PollCore() {
            var t = new CpuTelemetry();
            ulong v;
            if (tjMax == 0 && Msr(IA32_TEMPERATURE_TARGET, out v)) { tjMax = (int)((v >> 16) & 0xFF); if (tjMax == 0) tjMax = 100; }
            t.TjMax = tjMax;
            if (tjMax > 0 && Msr(IA32_PACKAGE_THERM_STATUS, out v) && (v & 0x80000000u) != 0) {
                t.DieTemp = tjMax - (int)((v >> 16) & 0x7F);

                if ((v & (1u << 0)) != 0) t.Throttle = "thermal";
                else if ((v & (1u << 2)) != 0) t.Throttle = "PROCHOT";
                else if ((v & (1u << 10)) != 0) t.Throttle = "power limit";
            }


            if (t.Throttle.Length == 0 && Msr(IA32_THERM_STATUS, out v) && (v & (1u << 12)) != 0) t.Throttle = "current limit";
            if (powerUnit == 0 && Msr(MSR_RAPL_POWER_UNIT, out v)) {
                powerUnit = 1.0 / (1 << (int)(v & 0xF));
                EnergyUnit = 1.0 / (1 << (int)((v >> 8) & 0x1F));
            }
            if (powerUnit > 0 && Msr(MSR_PKG_POWER_LIMIT, out v)) {
                t.Pl1 = (v & 0x7FFF) * powerUnit; t.Pl1On = (v & (1ul << 15)) != 0;
                t.Pl2 = ((v >> 32) & 0x7FFF) * powerUnit; t.Pl2On = (v & (1ul << 47)) != 0;
                t.PlLocked = (v & (1ul << 63)) != 0;
            }
            if (EnergyUnit > 0 && Msr(MSR_PKG_ENERGY_STATUS, out v)) t.Watts = Power(v & 0xFFFFFFFFu);


            if (!baseTried) {
                baseTried = true;
                if (Msr(MSR_PLATFORM_INFO, out v)) BaseMhz = ((v >> 8) & 0xFF) * 100.0;
                if (BaseMhz <= 0) BaseMhz = NominalMhz();
            }
            t.Mhz = Clock();
            return t;
        }
    }

    sealed class AmdCpu : CpuRegisters {
        const uint THM_TCON_CUR_TMP = 0x00059800;
        const uint MSR_PWR_UNIT = 0xC0010299, MSR_PKG_ENERGY_STAT = 0xC001029B;
        bool baseTried;
        readonly Mutex pci;
        public AmdCpu(PawnIoModule module) : base(module) { try { pci = new Mutex(false, @"Global\Access_PCI"); } catch { } }
        public override string Describe { get { return "AMD SMN + MSR"; } }

        bool Smn(uint addr, out uint v) {
            v = 0;
            bool held = false;
            try {
                if (pci != null) { try { held = pci.WaitOne(250); } catch (AbandonedMutexException) { held = true; } }
                var o = new ulong[1];
                if (!Module.Call("ioctl_read_smn", new ulong[] { addr }, o)) return false;
                v = (uint)o[0];
                return true;
            } finally { if (held) { try { pci.ReleaseMutex(); } catch { } } }
        }

        protected override CpuTelemetry PollCore() {
            var t = new CpuTelemetry();
            uint r;
            if (Smn(THM_TCON_CUR_TMP, out r)) {

                double tctl = ((r >> 21) & 0x7FF) * 0.125;
                if ((r & (1u << 19)) != 0) tctl -= 49;
                if (tctl > 0 && tctl < 130) t.DieTemp = tctl;
            }
            ulong v;
            if (EnergyUnit == 0 && Msr(MSR_PWR_UNIT, out v)) EnergyUnit = 1.0 / (1 << (int)((v >> 8) & 0x1F));
            if (EnergyUnit > 0 && Msr(MSR_PKG_ENERGY_STAT, out v)) t.Watts = Power(v & 0xFFFFFFFFu);

            if (!baseTried) { baseTried = true; BaseMhz = NominalMhz(); }
            t.Mhz = Clock();
            return t;
        }
        public override void Dispose() { base.Dispose(); try { if (pci != null) pci.Close(); } catch { } }
    }


    public sealed class DemoCpu : CpuRegisters {
        readonly Random rnd = new Random();
        double temp = 52;
        public DemoCpu() : base(null) { }
        public override string Describe { get { return "simulated"; } }
        protected override CpuTelemetry PollCore() {
            temp = Math.Max(38, Math.Min(88, temp + rnd.Next(-2, 3)));
            return new CpuTelemetry { DieTemp = temp, TjMax = 110, Pl1 = 45, Pl2 = 80, Pl1On = true, Pl2On = true, Watts = 12 + rnd.Next(0, 9), Mhz = 2300 + rnd.Next(0, 1800), Throttle = temp > 84 ? "power limit" : "" };
        }
    }
}
