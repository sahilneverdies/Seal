


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Seal {

    public sealed class SensorSnapshot {
        public double CpuTemp = double.NaN, CpuLoad = double.NaN, CpuMhz = double.NaN, CpuWatts = double.NaN;
        public double AcpiTemp = double.NaN;
        public double CpuTempNow = double.NaN;
        public bool CpuFromDriver;
        public double Pl1 = double.NaN, Pl2 = double.NaN;
        public string Throttle = "";
        public DateTime GpuRead = DateTime.MinValue;
        public double GpuTemp = double.NaN, GpuLoad = double.NaN, GpuWatts = double.NaN, GpuMhz = double.NaN;
        public bool OnBattery;
        public int BatteryPercent = -1;
    }

    public sealed class Sensors : IDisposable {
        PerformanceCounter[] thermals = new PerformanceCounter[0];
        PerformanceCounter cpuUtil, cpuFreq, cpuPerf, cpuPower;
        string nvsmi;
        int nvFail;

        int gpuQuiet;
        DateTime lastNv = DateTime.MinValue;
        public volatile bool SkipGpu;
        const int GpuIdleMs = 30000, GpuStaleMs = 45000;
        readonly object sync = new object();
        SensorSnapshot last = new SensorSnapshot();
        Thread worker;
        volatile bool stop;
        volatile int intervalMs = 2000;

        readonly ManualResetEvent wake = new ManualResetEvent(false);
        public event Action<SensorSnapshot> Updated;


        public Func<CpuRegisters> CpuSource;
        int cpuPollFailures;
        bool disagreementLogged;

        public Sensors() { }


        void InitCounters() {
            try {

                var cat = new PerformanceCounterCategory("Thermal Zone Information");
                string[] inst = cat.GetInstanceNames();
                Array.Sort(inst);
                var live = new List<PerformanceCounter>();
                foreach (string name in inst) {
                    PerformanceCounter c = null;
                    try {
                        c = new PerformanceCounter("Thermal Zone Information", "Temperature", name, true);
                        double k = c.NextValue();
                        if (k < 283 || k > 398) { c.Dispose(); continue; }
                        live.Add(c);
                    } catch { if (c != null) try { c.Dispose(); } catch { } }
                }
                thermals = live.ToArray();
                if (thermals.Length > 0) Log.Write("thermal zones: " + thermals.Length + " usable of " + inst.Length + ", hottest wins each poll");
                else Log.Write("no usable thermal zone among " + inst.Length + "; CPU temperature unavailable");
            } catch (Exception ex) { Log.Write("no thermal zone counter: " + ex.Message); }
            try { cpuUtil = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true); cpuUtil.NextValue(); } catch (Exception ex) { Log.Write("no cpu util counter: " + ex.Message); }

            try { cpuFreq = new PerformanceCounter("Processor Information", "Processor Frequency", "_Total", true); cpuFreq.NextValue(); } catch { }
            try { cpuPerf = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true); cpuPerf.NextValue(); } catch (Exception ex) { Log.Write("no cpu performance counter: " + ex.Message); }

            try {
                var cat = new PerformanceCounterCategory("Energy Meter");
                foreach (string inst in cat.GetInstanceNames())
                    if (inst.IndexOf("PKG", StringComparison.OrdinalIgnoreCase) >= 0) { cpuPower = new PerformanceCounter("Energy Meter", "Power", inst, true); cpuPower.NextValue(); Log.Write("cpu power counter: " + inst); break; }
            } catch (Exception ex) { Log.Write("no cpu power counter: " + ex.Message); }
            foreach (string p in new string[] { Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"), @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe" })
                if (File.Exists(p)) { nvsmi = p; break; }
            if (nvsmi == null) Log.Write("nvidia-smi not found; GPU stats disabled");
            else {
                try {
                    using (var q = new System.Management.ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController"))
                        foreach (System.Management.ManagementObject o in q.Get()) {
                            string id = "" + o["PNPDeviceID"];
                            if (id.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0) { nvDevice = id; break; }
                        }
                } catch (Exception ex) { Log.Write("nvidia adapter lookup: " + ex.Message); }
            }
        }

        public void SetInterval(int ms) {
            int now = Math.Max(500, ms);
            if (now == intervalMs) return;
            intervalMs = now;
            if (now < 3000) wake.Set();
        }

        public void Start() {
            if (worker != null) return;
            worker = new Thread(Loop) { IsBackground = true, Name = "sensors" };
            worker.Start();
        }

        void Loop() {
            InitCounters();

            int warm = 3;
            while (!stop) {
                wake.Reset();
                var s = new SensorSnapshot();

                double driverWatts = double.NaN, die = double.NaN, driverMhz = double.NaN;
                CpuRegisters cpu = CpuSource == null ? null : CpuSource();
                if (cpu != null) {
                    try {
                        CpuTelemetry ct = cpu.Poll();
                        die = ct.DieTemp;
                        if (ct.Pl1On) s.Pl1 = ct.Pl1;
                        if (ct.Pl2On) s.Pl2 = ct.Pl2;
                        s.Throttle = ct.Throttle ?? "";
                        driverWatts = ct.Watts;
                        driverMhz = ct.Mhz;
                    } catch (Exception ex) { if (cpuPollFailures++ == 0) Log.Write("driver cpu poll: " + ex.Message); }
                }

                double hot = double.NaN;
                for (int i = 0; i < thermals.Length; i++) {
                    try {
                        double k = thermals[i].NextValue();
                        if (k >= 283 && k <= 398 && (double.IsNaN(hot) || k > hot)) hot = k;
                    } catch { }
                }
                if (!double.IsNaN(hot)) s.AcpiTemp = Math.Round(hot - 273.15, 1);


                s.CpuFromDriver = !double.IsNaN(die);
                s.CpuTempNow = s.CpuFromDriver ? die : s.AcpiTemp;


                if (s.CpuFromDriver != tempFromDriver) { tempFromDriver = s.CpuFromDriver; recentCount = 0; }
                s.CpuTemp = Steady(s.CpuTempNow);
                if (!disagreementLogged && s.CpuFromDriver && !double.IsNaN(s.AcpiTemp) && Math.Abs(s.CpuTemp - s.AcpiTemp) > 15) {
                    disagreementLogged = true;
                    Log.Write("cpu temperature: the driver reads " + s.CpuTemp.ToString("0") + ", the ACPI zone " + s.AcpiTemp.ToString("0") + "; the driver's number is used");
                }
                try { if (cpuUtil != null) s.CpuLoad = Math.Min(100, cpuUtil.NextValue()); } catch { }
                try {
                    if (cpuFreq != null) {
                        double base_ = cpuFreq.NextValue();
                        double pct = double.NaN;
                        try { if (cpuPerf != null) pct = cpuPerf.NextValue(); } catch { }

                        s.CpuMhz = (!double.IsNaN(pct) && pct > 1 && pct < 500) ? base_ * pct / 100.0 : double.NaN;
                    }


                    if (!double.IsNaN(driverMhz)) s.CpuMhz = driverMhz;
                } catch { }

                try {
                    bool fromDriver = !double.IsNaN(driverWatts);
                    if (fromDriver != wattsFromDriver) { wattsFromDriver = fromDriver; recentWattsCount = 0; }
                    if (fromDriver) s.CpuWatts = Watts(driverWatts);
                    else if (cpuPower != null) s.CpuWatts = Watts(cpuPower.NextValue() / 1000.0);
                } catch { }
                try {
                    var ps = System.Windows.Forms.SystemInformation.PowerStatus;
                    s.OnBattery = ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
                    s.BatteryPercent = (int)Math.Round(ps.BatteryLifePercent * 100);
                } catch { }

                if (nvsmi != null && nvFail < 5 && !SkipGpu && DueForGpu(s)) { lastNv = DateTime.Now; ReadNvidia(s); NoteGpuActivity(s); if (!double.IsNaN(s.GpuTemp)) s.GpuRead = DateTime.Now; }
                else lock (sync) { s.GpuTemp = last.GpuTemp; s.GpuLoad = last.GpuLoad; s.GpuWatts = last.GpuWatts; s.GpuMhz = last.GpuMhz; s.GpuRead = last.GpuRead; }


                if (s.GpuRead != DateTime.MinValue && (DateTime.Now - s.GpuRead).TotalMilliseconds > GpuStaleMs) { s.GpuTemp = double.NaN; s.GpuLoad = double.NaN; s.GpuWatts = double.NaN; s.GpuMhz = double.NaN; }
                lock (sync) last = s;
                var h = Updated; if (h != null) { try { h(s); } catch { } }
                wake.WaitOne(warm > 0 ? Math.Min(1000, intervalMs) : intervalMs);
                if (warm > 0) warm--;
            }
        }

        readonly double[] recent = new double[3];
        int recentCount;
        bool tempFromDriver;
        double Steady(double now) {
            if (double.IsNaN(now)) { recentCount = 0; return now; }
            recent[2] = recent[1];
            recent[1] = recent[0];
            recent[0] = now;
            if (recentCount < 3) { recentCount++; if (recentCount < 3) return now; }
            double a = recent[0], b = recent[1], c = recent[2];
            return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        }

        public static double AcpiZoneOnce() {
            double hot = double.NaN;
            try {
                var cat = new PerformanceCounterCategory("Thermal Zone Information");
                foreach (string n in cat.GetInstanceNames())
                    using (var pc = new PerformanceCounter("Thermal Zone Information", "Temperature", n, true)) { double k = pc.NextValue(); if (k >= 283 && k <= 398 && (double.IsNaN(hot) || k > hot)) hot = k; }
            } catch { }
            return double.IsNaN(hot) ? hot : Math.Round(hot - 273.15);
        }

        bool DueForGpu(SensorSnapshot s) {
            bool cpuBusy = !double.IsNaN(s.CpuLoad) && s.CpuLoad > 25;
            double want = gpuQuiet >= 3 && !cpuBusy ? GpuIdleMs : Math.Max(4000, intervalMs * 2);
            if ((DateTime.Now - lastNv).TotalMilliseconds < want) return false;

            if (GpuAsleep()) { lastNv = DateTime.Now; lock (sync) { if (last != null && last.GpuRead != DateTime.MinValue) last.GpuRead = DateTime.Now; } return false; }
            return true;
        }


        string nvDevice;
        [StructLayout(LayoutKind.Sequential)] struct DevPropKey { public Guid Fmtid; public uint Pid; }
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint inst, string id, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_DevNode_PropertyW(uint inst, ref DevPropKey key, out uint type, byte[] buf, ref uint size, uint flags);


        bool GpuAsleep() {
            if (nvDevice == null) return false;
            try {
                uint inst;
                if (CM_Locate_DevNodeW(out inst, nvDevice, 0) != 0) return false;
                var key = new DevPropKey { Fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 32 };
                var buf = new byte[64];
                uint type, size = (uint)buf.Length;
                if (CM_Get_DevNode_PropertyW(inst, ref key, out type, buf, ref size, 0) != 0 || size < 8) return false;
                return BitConverter.ToInt32(buf, 4) == 4;
            } catch { return false; }
        }
        void NoteGpuActivity(SensorSnapshot s) {
            bool busy = (!double.IsNaN(s.GpuLoad) && s.GpuLoad > 1) || (!double.IsNaN(s.GpuWatts) && s.GpuWatts >= 12);
            if (busy) gpuQuiet = 0;
            else if (gpuQuiet < 100) gpuQuiet++;
        }

        double wattsAvg = double.NaN;
        static readonly double WattsCeiling = 200;
        readonly double[] recentWatts = new double[3];
        int recentWattsCount;
        bool wattsFromDriver;

        double Watts(double w) {
            if (w <= 0 || w > WattsCeiling) return wattsAvg;
            recentWatts[2] = recentWatts[1]; recentWatts[1] = recentWatts[0]; recentWatts[0] = w;
            if (recentWattsCount < 3) { recentWattsCount++; if (recentWattsCount < 3) { wattsAvg = w; return w; } }
            double x = recentWatts[0], y = recentWatts[1], z = recentWatts[2];
            wattsAvg = Math.Max(Math.Min(x, y), Math.Min(Math.Max(x, y), z));
            return wattsAvg;
        }
        void ReadNvidia(SensorSnapshot s) {
            try {
                var psi = new ProcessStartInfo(nvsmi, "--query-gpu=temperature.gpu,utilization.gpu,power.draw,clocks.gr --format=csv,noheader,nounits") {
                    CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                };
                using (var p = Process.Start(psi)) {
                    string line = null;
                    p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (line == null && e.Data != null) line = e.Data; };
                    p.ErrorDataReceived += delegate { };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(2500)) {
                        try { p.Kill(); } catch { }
                        p.WaitForExit(2000);
                        if (nvFail == 0) Log.Write("nvidia-smi query timed out");
                        nvFail++;
                        return;
                    }
                    p.WaitForExit();
                    if (string.IsNullOrEmpty(line)) { nvFail++; return; }
                    string[] parts = line.Split(',');
                    if (parts.Length != 4) { nvFail++; return; }
                    double v;
                    if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0 && v < 130) s.GpuTemp = v;
                    if (double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0 && v <= 100) s.GpuLoad = v;
                    if (double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0 && v < 250) s.GpuWatts = v;
                    if (double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 100) s.GpuMhz = v;
                    nvFail = 0;
                }
            } catch (Exception ex) { nvFail++; if (nvFail == 5) Log.Write("nvidia-smi disabled: " + ex.Message); }
        }

        public void Dispose() {
            stop = true;
            wake.Set();
            try { for (int i = 0; i < thermals.Length; i++) { try { thermals[i].Dispose(); } catch { } } if (cpuUtil != null) cpuUtil.Dispose(); if (cpuFreq != null) cpuFreq.Dispose(); if (cpuPower != null) cpuPower.Dispose(); } catch { }
        }
    }
}

