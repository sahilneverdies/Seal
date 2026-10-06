

using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace Seal {


    public interface IEcPorts {
        int In(byte port, out byte value);
        int Out(byte port, byte value);
    }


    public sealed class PawnIoEcPorts : IEcPorts, IDisposable {
        readonly PawnIoModule m;
        public PawnIoEcPorts(PawnIoModule module) { m = module; }
        public int In(byte port, out byte value) {
            var o = new ulong[1]; int n;
            int rc = m.Execute("ioctl_pio_read", new ulong[] { port }, o, out n);
            value = rc == 0 ? (byte)o[0] : (byte)0;
            return rc;
        }
        public int Out(byte port, byte value) { int n; return m.Execute("ioctl_pio_write", new ulong[] { port, value }, null, out n); }
        public void Dispose() { m.Dispose(); }
    }


    public sealed class EcMap {
        public string Name;
        public byte CpuTemp = 0x57, GpuTemp = 0xB7;
        public byte Rpm1 = 0xB0, Rpm2 = 0xB2;
        public byte FanSet1 = 0x34, FanSet2 = 0x35;
        public byte FanSetPct1 = 0x2C, FanSetPct2 = 0x2D;

        public bool UsePercent;
        public byte Manual = 0x62, ManualOn = 0x06, ManualOff = 0x00;

        public byte Countdown = 0x63, CountdownRelease = 0x01, CountdownHold = 0xFF;
        public byte Mode = 0x95, Charge = 0x96;
        byte[] writable;

        public bool MayWrite(byte reg) {
            if (writable == null) writable = new byte[] { FanSet1, FanSet2, FanSetPct1, FanSetPct2, Manual, Countdown };
            foreach (byte w in writable) if (w == reg) return true;
            return false;
        }

        public byte Encode(int level, int ceiling) {
            if (level <= 0) return 0;
            if (!UsePercent) return (byte)Math.Min(255, level);
            return (byte)Math.Max(0, Math.Min(100, (int)Math.Round(100.0 * level / Math.Max(1, ceiling))));
        }

        public static EcMap Legacy() { return new EcMap { Name = "OMEN 2018-2022 (OmenMon / omen-fan map)" }; }
    }


    public sealed class EcReading {
        public int Cpu = -1, Gpu = -1, Rpm1 = -1, Rpm2 = -1, Manual = -1, Countdown = -1, Mode = -1, Charge = -1;
        public bool Any { get { return Cpu >= 0 || Rpm1 >= 0 || Manual >= 0; } }
    }

    public sealed class EmbeddedController : IDisposable {
        const byte DataPort = 0x62, CommandPort = 0x66;
        const byte CmdRead = 0x80, CmdWrite = 0x81;
        const byte Obf = 0x01, Ibf = 0x02;
        const int WaitPolls = 400;
        const int MutexWaitMs = 250;
        const int TimeoutsBeforeRest = 5;
        static readonly TimeSpan Rest = TimeSpan.FromMinutes(10);

        public readonly EcMap Map;
        readonly IEcPorts ports;
        readonly Mutex mutex;
        readonly object sync = new object();
        int timeouts;
        DateTime restUntil = DateTime.MinValue;
        public int Timeouts { get { return timeouts; } }
        public string LastError = "";
        readonly Dictionary<byte, byte> lastWritten = new Dictionary<byte, byte>();
        readonly Dictionary<byte, DateTime> lastWrittenAt = new Dictionary<byte, DateTime>();

        public EmbeddedController(IEcPorts ports, EcMap map) {
            this.ports = ports;
            Map = map;
            mutex = OpenMutex(@"Global\Access_EC");
        }

        static Mutex OpenMutex(string name) {
            try {
                var sec = new MutexSecurity();
                sec.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow));
                bool created;
                return new Mutex(false, name, out created, sec);
            } catch (UnauthorizedAccessException) {
                try { return Mutex.OpenExisting(name, MutexRights.Synchronize | MutexRights.Modify); } catch { return null; }
            } catch { return null; }
        }


        public bool Resting { get { return DateTime.Now < restUntil; } }


        bool Status(out byte s) { return ports.In(CommandPort, out s) == 0; }
        bool WaitFor(byte bit, bool set) {
            byte s;
            for (int i = 0; i < WaitPolls; i++) {
                if (!Status(out s)) return false;
                if (((s & bit) != 0) == set) return true;
                if (i >= 20) Thread.Sleep(1);
            }
            return false;
        }


        void Drain() {
            byte s, junk;
            if (Status(out s) && (s & Obf) != 0) ports.In(DataPort, out junk);
        }
        bool ReadRaw(byte reg, out byte value) {
            value = 0;
            Drain();
            if (!WaitFor(Ibf, false) || ports.Out(CommandPort, CmdRead) != 0) return false;
            if (!WaitFor(Ibf, false) || ports.Out(DataPort, reg) != 0) return false;
            if (!WaitFor(Obf, true)) return false;
            return ports.In(DataPort, out value) == 0;
        }
        bool WriteRaw(byte reg, byte value) {
            if (!WaitFor(Ibf, false) || ports.Out(CommandPort, CmdWrite) != 0) return false;
            if (!WaitFor(Ibf, false) || ports.Out(DataPort, reg) != 0) return false;
            if (!WaitFor(Ibf, false) || ports.Out(DataPort, value) != 0) return false;
            return WaitFor(Ibf, false);
        }

        bool Locked(Func<bool> body, string what) {
            if (Resting) { LastError = "EC resting after " + timeouts + " timeouts"; return false; }
            bool held = false;
            lock (sync) {
                try {
                    if (mutex != null) {
                        try { held = mutex.WaitOne(MutexWaitMs); } catch (AbandonedMutexException) { held = true; }
                        if (!held) { LastError = "EC busy (another program holds it)"; return false; }
                    }
                    bool ok = body();
                    if (ok) { timeouts = 0; LastError = ""; }
                    else {
                        timeouts++;
                        if (LastError.Length == 0) LastError = "EC did not answer (" + what + ")";
                        if (timeouts >= TimeoutsBeforeRest) { restUntil = DateTime.Now + Rest; Log.Write("EC: " + timeouts + " timeouts in a row; leaving it alone for " + Rest.TotalMinutes + " minutes"); }
                    }
                    return ok;
                } catch (Exception ex) { LastError = ex.Message; return false; }
                finally { if (held) { try { mutex.ReleaseMutex(); } catch { } } }
            }
        }


        public bool ReadByte(byte reg, out byte value) { byte v = 0; bool ok = Locked(delegate { return ReadRaw(reg, out v); }, "read 0x" + reg.ToString("X2")); value = v; return ok; }


        void Forget() { lastWritten.Clear(); }

        public bool WriteByte(byte reg, byte value) {
            if (!Map.MayWrite(reg)) { LastError = "0x" + reg.ToString("X2") + " is not a register this map allows writing"; Log.Write("EC: refused write to " + LastError); return false; }
            byte had; DateTime at;

            if (reg != Map.Countdown
                && lastWritten.TryGetValue(reg, out had) && had == value && lastWrittenAt.TryGetValue(reg, out at) && (DateTime.Now - at).TotalSeconds < 5) return true;
            bool ok = Locked(delegate { return WriteRaw(reg, value); }, "write 0x" + reg.ToString("X2"));
            if (ok) { lastWritten[reg] = value; lastWrittenAt[reg] = DateTime.Now; }
            return ok;
        }


        public EcReading Read() {
            var r = new EcReading();
            Locked(delegate {
                byte lo, hi, v;
                if (ReadRaw(Map.CpuTemp, out v)) r.Cpu = v;
                if (ReadRaw(Map.GpuTemp, out v)) r.Gpu = v;
                if (ReadRaw(Map.Rpm1, out lo) && ReadRaw((byte)(Map.Rpm1 + 1), out hi)) r.Rpm1 = lo | (hi << 8);
                if (ReadRaw(Map.Rpm2, out lo) && ReadRaw((byte)(Map.Rpm2 + 1), out hi)) r.Rpm2 = lo | (hi << 8);
                if (ReadRaw(Map.Manual, out v)) r.Manual = v;
                if (ReadRaw(Map.Countdown, out v)) r.Countdown = v;
                if (ReadRaw(Map.Mode, out v)) r.Mode = v;
                if (ReadRaw(Map.Charge, out v)) r.Charge = v;
                return r.Any;
            }, "snapshot");
            return r;
        }

        public bool HoldFans(int level1, int level2, int ceiling) {
            Claim();
            byte l1 = Map.Encode(level1, ceiling), l2 = Map.Encode(level2, ceiling);
            byte r1 = Map.UsePercent ? Map.FanSetPct1 : Map.FanSet1, r2 = Map.UsePercent ? Map.FanSetPct2 : Map.FanSet2;
            if (!WriteByte(Map.Manual, Map.ManualOn)) return false;
            if (!WriteByte(r1, l1) || !WriteByte(r2, l2)) return false;
            if (!WriteByte(Map.Countdown, Map.CountdownHold)) return false;
            byte check;
            if (!ReadByte(Map.Manual, out check)) return false;
            if (check != Map.ManualOn) { LastError = "manual register reads 0x" + check.ToString("X2") + " after writing 0x" + Map.ManualOn.ToString("X2") + "; this EC does not follow the map"; return false; }
            return true;
        }

        int entryManual = -1, entryCountdown = -1;
        void Claim() {
            if (entryManual >= 0) return;
            byte v;
            if (ReadByte(Map.Manual, out v)) entryManual = v;
            if (ReadByte(Map.Countdown, out v)) entryCountdown = v;
        }

        public bool ReleaseFans() {
            Forget();

            bool ok = true;
            if (entryManual >= 0) ok &= WriteByte(Map.Manual, (byte)entryManual);
            if (entryCountdown >= 0) ok &= WriteByte(Map.Countdown, (byte)entryCountdown);
            entryManual = entryCountdown = -1;
            return ok;
        }

        public bool ConfirmFansRunning(int safeLevel, int ceiling) {
            Thread.Sleep(3000);
            EcReading r = Read();
            if (r.Rpm1 > 300 || r.Rpm2 > 300) return true;


            Log.Write("EC: fans " + (r.Rpm1 < 0 ? "could not be read" : "still read " + r.Rpm1 + "/" + r.Rpm2 + " rpm") + " after handing control back; forcing " + safeLevel);
            Forget();
            HoldFans(safeLevel, safeLevel, ceiling);
            return false;
        }

        public string ProbeFanWrite(int safeLevel, int ceiling, out int pair) {
            pair = 0;
            var sb = new StringBuilder();
            Claim();
            int rest = AverageRpm();
            if (rest < 0) return "  the tachometers did not answer, so there is nothing to measure a change against\n";
            sb.AppendLine("  fans at rest:    " + rest + " rpm");
            if (rest > 4200) sb.AppendLine("  NOTE: the fans are already fast, so a rise may not be visible. Run this on an idle machine.");
            try {
                for (int pass = 0; pass < 2; pass++) {
                    bool pct = pass == 1;
                    byte r1 = pct ? Map.FanSetPct1 : Map.FanSet1, r2 = pct ? Map.FanSetPct2 : Map.FanSet2;
                    byte v = pct ? (byte)80 : (byte)50;
                    string what = "0x" + r1.ToString("X2") + "/0x" + r2.ToString("X2") + " = " + v + (pct ? "%" : " (rpm/100)");
                    Forget();

                    Claim();
                    if (!(WriteByte(Map.Manual, Map.ManualOn) && WriteByte(Map.Countdown, Map.CountdownHold)
                        && WriteByte(r1, v) && WriteByte(r2, v))) {
                        sb.AppendLine("  " + what.PadRight(27) + "the write was refused (" + LastError + ")");
                        continue;
                    }
                    Thread.Sleep(5000);
                    int now = AverageRpm();
                    int rise = now - rest;
                    sb.AppendLine("  " + what.PadRight(27) + now + " rpm, " + (rise >= 0 ? "+" : "") + rise
                        + (rise > 400 ? "   <-- this pair drives the fans on this board" : "   no change"));
                    if (rise > 400 && pair == 0) pair = pct ? 2 : 1;
                    ReleaseFans();
                    Thread.Sleep(2500);
                }
            } finally {
                bool released = ReleaseFans();

                bool spinning = ConfirmFansRunning(safeLevel, ceiling);
                if (released && spinning) sb.AppendLine("  fans handed back to the controller and turning again.");
                else if (spinning) sb.AppendLine("  handover reported an error (" + LastError + ") but the fans are turning.");
                else sb.AppendLine("  fans did not restart on their own, so they are being held at " + safeLevel + " instead. Please say so in the issue.");
            }
            return sb.ToString();
        }
        int AverageRpm() {
            int sum = 0, n = 0;
            for (int i = 0; i < 3; i++) {
                EcReading r = Read();
                if (r.Rpm1 >= 0) { sum += r.Rpm1; n++; }
                Thread.Sleep(400);
            }
            return n > 0 ? sum / n : -1;
        }

        public bool Verify(int[] mailboxRpm, double dieTemp, out string why) {
            EcReading r = Read();
            if (!r.Any) { why = LastError.Length > 0 ? LastError : "the EC did not answer"; return false; }
            if (r.Manual != Map.ManualOff && r.Manual != Map.ManualOn) {
                why = "0x" + Map.Manual.ToString("X2") + " reads 0x" + (r.Manual < 0 ? "??" : r.Manual.ToString("X2")) + ", which is not a fan-control state";
                return false;
            }

            bool tempAbsent = r.Cpu == 0;
            if (!tempAbsent) {
                if (r.Cpu < 20 || r.Cpu > 110) { why = "0x" + Map.CpuTemp.ToString("X2") + " reads " + r.Cpu + ", which is not a temperature"; return false; }
                if (!double.IsNaN(dieTemp) && Math.Abs(r.Cpu - dieTemp) > 25) {
                    why = "it reads " + r.Cpu + " where the CPU itself reads " + dieTemp.ToString("0");
                    return false;
                }
            }
            if (r.Rpm1 < 0 || r.Rpm1 > 9000 || r.Rpm2 < 0 || r.Rpm2 > 9000) { why = "fan speeds of " + r.Rpm1 + " and " + r.Rpm2 + " are not rpm"; return false; }

            bool mailboxKnown = mailboxRpm != null && mailboxRpm.Length > 1 && (mailboxRpm[0] > 0 || (tempAbsent && mailboxRpm[0] == 0));
            if (mailboxKnown) {
                int want = mailboxRpm[0] * 100, slack = Math.Max(500, want / 4);
                if (Math.Abs(r.Rpm1 - want) > slack) { why = "it reads " + r.Rpm1 + " rpm where the firmware reads " + want; return false; }
            } else if (tempAbsent) { why = "no temperature at 0x" + Map.CpuTemp.ToString("X2") + " and no firmware fan speed to check the tachometers against"; return false; }
            why = (tempAbsent ? "no temperature register" : "CPU " + r.Cpu + " C") + ", fans " + r.Rpm1 + "/" + r.Rpm2 + " rpm, control 0x" + r.Manual.ToString("X2");
            return true;
        }

        public void Dispose() {
            try { var d = ports as IDisposable; if (d != null) d.Dispose(); } catch { }
            try { if (mutex != null) mutex.Close(); } catch { }
        }
    }


    public sealed class DemoEcPorts : IEcPorts {
        readonly byte[] ram = new byte[256];
        readonly Random rnd = new Random();
        byte status;
        int phase;
        byte address, output;
        DateTime lastTick = DateTime.Now;
        public DemoEcPorts() {
            ram[0x57] = 48; ram[0xB7] = 41;
            SetRpm(0xB0, 2650); SetRpm(0xB2, 2480);
            ram[0x63] = 0x78; ram[0x95] = 0x30; ram[0x96] = 100;
        }
        void SetRpm(int at, int rpm) { ram[at] = (byte)(rpm & 0xFF); ram[at + 1] = (byte)(rpm >> 8); }
        void Tick() {
            if ((DateTime.Now - lastTick).TotalSeconds < 1) return;
            lastTick = DateTime.Now;
            ram[0x57] = (byte)Math.Max(36, Math.Min(90, ram[0x57] + rnd.Next(-1, 2)));
            if (ram[0x63] > 0) ram[0x63]--;
            if (ram[0x62] == 0x06) { SetRpm(0xB0, ram[0x34] * 100); SetRpm(0xB2, ram[0x35] * 100); }
        }
        public int In(byte port, out byte value) {
            Tick();
            if (port == 0x66) { value = status; return 0; }
            value = output; status &= unchecked((byte)~0x01);
            return 0;
        }
        public int Out(byte port, byte value) {
            if (port == 0x66) { phase = value == 0x80 ? 1 : value == 0x81 ? 2 : 0; return 0; }
            switch (phase) {
                case 1: output = ram[value]; status |= 0x01; phase = 0; break;
                case 2: address = value; phase = 3; break;
                case 3: ram[address] = value; phase = 0; break;
            }
            return 0;
        }
    }
}
