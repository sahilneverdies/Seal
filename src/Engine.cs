

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Seal {

    public enum FanMode { Auto = 0, Max = 1, Manual = 2, Custom = 3 }
    public enum KeyAction { Cycle = 0, Show = 1, MaxFan = 2, Off = 3, Run = 4 }
    public enum GpuLevel { Base = 0, Boost = 1, Max = 2 }


    public sealed class ModeProfile {
        public FanMode Fan = FanMode.Auto;
        public int Fan1 = 30, Fan2 = 30;
        public int[] CurveLevels;
        public int[] GpuCurveLevels;
        public bool CurveLinked = true;
        public int CurveFloor = 0;
        public int CurveRamp = 5;
        public int TdpOffset = 0;
        public GpuLevel Gpu = GpuLevel.Boost;
        public bool GpuAuto = true;
        public void Apply(string k, string v) {
            int n;
            bool b;
            switch (k) {
                case "Fan": if (Settings.TryInt(v, out n)) Fan = (FanMode)Math.Max(0, Math.Min(3, n)); break;
                case "Curve": { var lv = ParseCurve(v); if (lv != null) CurveLevels = lv; break; }
                case "GpuCurve": { var lv = ParseCurve(v); if (lv != null) GpuCurveLevels = lv; break; }
                case "CurveLink": if (bool.TryParse(v, out b)) CurveLinked = b; break;
                case "CurveFloor": if (Settings.TryInt(v, out n)) CurveFloor = Math.Max(0, Math.Min(99, n)); break;
                case "CurveRamp": if (Settings.TryInt(v, out n)) CurveRamp = Math.Max(1, Math.Min(10, n)); break;
                case "Fan1": if (Settings.TryInt(v, out n)) Fan1 = n; break;
                case "Fan2": if (Settings.TryInt(v, out n)) Fan2 = n; break;
                case "TdpOffset": if (Settings.TryInt(v, out n)) TdpOffset = Math.Max(0, Math.Min(30, n)); break;
                case "Gpu": if (Settings.TryInt(v, out n)) Gpu = (GpuLevel)Math.Max(0, Math.Min(2, n)); break;
                case "GpuAuto": if (bool.TryParse(v, out b)) GpuAuto = b; break;
            }
        }
        static int[] ParseCurve(string v) {
            var parts = v.Split(',');
            if (parts.Length != Engine.CurveTemps.Length) return null;
            var lv = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) if (!Settings.TryInt(parts[i].Trim(), out lv[i])) return null;
            return lv;
        }
        static string JoinCurve(int[] lv) { return string.Join(",", Array.ConvertAll(lv, delegate(int x) { return x.ToString(); })); }
        public void Write(StringBuilder sb, string prefix) {
            sb.AppendLine(prefix + "Fan=" + (int)Fan);
            sb.AppendLine(prefix + "Fan1=" + Fan1);
            sb.AppendLine(prefix + "Fan2=" + Fan2);
            sb.AppendLine(prefix + "TdpOffset=" + TdpOffset);
            sb.AppendLine(prefix + "Gpu=" + (int)Gpu);
            sb.AppendLine(prefix + "GpuAuto=" + GpuAuto);
            if (CurveLevels != null) sb.AppendLine(prefix + "Curve=" + JoinCurve(CurveLevels));
            if (GpuCurveLevels != null) sb.AppendLine(prefix + "GpuCurve=" + JoinCurve(GpuCurveLevels));
            sb.AppendLine(prefix + "CurveLink=" + CurveLinked);
            sb.AppendLine(prefix + "CurveFloor=" + CurveFloor);
            sb.AppendLine(prefix + "CurveRamp=" + CurveRamp);
        }
    }

    public sealed class Settings {
        public int ModeIndex = 1;

        public readonly ModeProfile[] Modes = { new ModeProfile(), new ModeProfile(), new ModeProfile { TdpOffset = 15 } };
        public ModeProfile Cur { get { return Modes[Math.Max(0, Math.Min(2, ModeIndex))]; } }
        public FanMode Fan { get { return Cur.Fan; } set { Cur.Fan = value; } }
        public int Fan1 { get { return Cur.Fan1; } set { Cur.Fan1 = value; } }
        public int Fan2 { get { return Cur.Fan2; } set { Cur.Fan2 = value; } }
        public int TdpOffset { get { return Cur.TdpOffset; } set { Cur.TdpOffset = value; } }
        public GpuLevel Gpu { get { return Cur.Gpu; } set { Cur.Gpu = value; } }
        public bool GpuAuto { get { return Cur.GpuAuto; } set { Cur.GpuAuto = value; } }
        public KeyAction Key = KeyAction.Show;
        public uint KeyId = 0, KeyData = 0;
        public bool SuppressOgh = true;
        public bool Hotkeys = true;
        public bool EcoOnBattery = false;
        public bool SyncWinPower = true;
        public bool EcoCool = false;
        public int HeartbeatSec = 45;
        public bool MaxBackWhenCool = true;
        public int MaxStopAfterMin = 30;
        public bool ManualLinked = true;
        public long UpdateChecked = 0;
        public string CheckedFrom = "";
        public string LatestVersion = "";
        public int WinX = -1, WinY = -1;
        public bool StartHidden = false;
        public string Name = "";
        public int Light = -1;
        public string LightColors = "";
        public int LightLevel = 100;
        public int LightEffect = 0;
        public int LightSpeed = 3;
        public int ExternalEffect = 0;
        public int ExternalSpeed = 3;
        public int ExternalLevel = 100;
        public string ExternalColor = "3F8CFF";
        public int RefreshHz = 0;
        public bool LowHzOnBattery = false;
        public bool TrayTemp = true;
        public int PollMs = 2000;
        public bool TookWinLighting = false;
        public bool PerKeyReset = false;
        public int FanBeforeMax = 0;
        public bool InfoDismissed = false;
        public bool Guard = true;
        public int GuardCpu, GuardChassis;
        public int GuardLevel;
        public int GuardHold;
        public bool UpdateOnLaunch = true;
        public bool MemCleanWorkingSet = true;
        public bool MemCleanSystemCache = true;
        public bool MemCleanStandby = true;
        public bool MemCleanModified = true;
        public bool MemCleanCombined = false;
        public bool MemCleanRegistry = false;
        public int MemCleanIntervalMin = 0;
        public int MemCleanThresholdPct = 0;
        public bool MemCleanNotify = false;
        public bool KbdLighting = true;
        public bool Overlay = false;
        public bool OverlayPinned = true;
        public bool OverlayPerfPinned = false;
        public bool OverlayHorizontal = true;
        public bool OverlayShowCpu = true;
        public bool OverlayShowGpu = true;
        public bool OverlayShowRam = true;
        public bool OverlayShowFps = true;
        public bool OverlayShowUpload = true;
        public bool OverlayShowDownload = true;
        public bool OverlayShowPerf = true;
        public double OverlayX = -1;
        public double OverlayY = -1;
        public int OverlayOpacity = 90;
        public int OverlayPerfMode = 1;
        public double OverlayPerfX = -1;
        public double OverlayPerfY = -1;
        public string OverlayHotkey = "Shift+F2";
        public bool OverlayDock = true;
        public double OverlayDockX = -1, OverlayDockY = -1;
        public string KeyCommand = "";
        public string[] HotkeyText = new string[HotkeyTable.Count];
        public bool DriverUse = true;
        public int FanCeilingSeen;
        public bool MaxIgnored;
        public bool DriverInstalledBySeal = false;
        public bool DriverRestartPending = false;
        public string DriverNudgeDismissed = "";
        public bool NoPersist;
        public int SavedModeOverride = -1;

        static readonly string File_ = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Program.FileStem + ".state");

        public bool FirstRun;
        public int Ver;
        public const int FileVer = 2;
        public static Settings Load() {
            var s = new Settings();
            try {
                if (!File.Exists(File_)) { s.FirstRun = true; return s; }
                foreach (string raw in File.ReadAllLines(File_)) {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || eq < 1) continue;
                    s.Apply(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                }
                s.Migrate();
            } catch (Exception ex) { Log.Write("settings load: " + ex.Message); }
            return s;
        }

        void Migrate() {


            if (Ver < 2 && Key == KeyAction.Cycle) { Key = KeyAction.Show; Log.Write("settings: OMEN key moved to the new default (open the window)"); }
            if (Ver != FileVer) { Ver = FileVer; Save(); }
        }


        public void Apply(string k, string v) {
            var s = this;
            try {

                if (k.Length > 3 && k[0] == 'M' && char.IsDigit(k[1]) && k[2] == '.') { int mi = k[1] - '0'; if (mi >= 0 && mi < 3) Modes[mi].Apply(k.Substring(3), v); return; }
                if (k.StartsWith("Hotkey.", StringComparison.Ordinal)) {
                    int hi = Array.IndexOf(HotkeyTable.Keys, k.Substring(7));
                    if (hi >= 0) s.HotkeyText[hi] = v.Trim();
                    return;
                }

                if (k == "Fan" || k == "Fan1" || k == "Fan2" || k == "TdpOffset" || k == "Gpu" || k == "GpuAuto" || k == "Curve") { foreach (var m in Modes) m.Apply(k, v); return; }
                {
                    int n;
                    bool b;
                    switch (k) {
                        case "Ver": if (TryInt(v, out n)) s.Ver = n; break;
                        case "ModeIndex": if (TryInt(v, out n)) s.ModeIndex = Math.Max(0, Math.Min(2, n)); break;
                        case "EcoCool": if (bool.TryParse(v, out b)) s.EcoCool = b; break;
                        case "Key": if (TryInt(v, out n)) s.Key = (KeyAction)Math.Max(0, Math.Min(4, n)); break;
                        case "KeyId": if (TryInt(v, out n)) s.KeyId = (uint)n; break;
                        case "KeyData": if (TryInt(v, out n)) s.KeyData = (uint)n; break;
                        case "SuppressOgh": if (bool.TryParse(v, out b)) s.SuppressOgh = b; break;
                        case "Hotkeys": if (bool.TryParse(v, out b)) s.Hotkeys = b; break;
                        case "EcoOnBattery": if (bool.TryParse(v, out b)) s.EcoOnBattery = b; break;
                        case "SyncWinPower": if (bool.TryParse(v, out b)) s.SyncWinPower = b; break;
                        case "HeartbeatSec": if (TryInt(v, out n)) s.HeartbeatSec = Math.Max(10, Math.Min(110, n)); break;
                        case "MaxBackWhenCool": if (bool.TryParse(v, out b)) s.MaxBackWhenCool = b; break;
                        case "MaxStopAfterMin": if (TryInt(v, out n)) s.MaxStopAfterMin = Math.Max(0, Math.Min(240, n)); break;
                        case "ManualLinked": if (bool.TryParse(v, out b)) s.ManualLinked = b; break;
                        case "UpdateChecked": { long l; if (long.TryParse(v, out l)) s.UpdateChecked = l; break; }
                        case "CheckedFrom": s.CheckedFrom = v; break;
                        case "PollMs": { int pm; if (int.TryParse(v, out pm) && pm >= 500 && pm <= 5000) s.PollMs = pm; break; }
                        case "TookWinLighting": if (bool.TryParse(v, out b)) s.TookWinLighting = b; break;
                        case "PerKeyReset": if (bool.TryParse(v, out b)) s.PerKeyReset = b; break;
                        case "FanBeforeMax": if (TryInt(v, out n)) s.FanBeforeMax = Math.Max(0, Math.Min(3, n)); break;
                        case "LatestVersion": s.LatestVersion = v.Length > 24 ? v.Substring(0, 24) : v; break;
                        case "WinX": if (TryInt(v, out n)) s.WinX = n; break;
                        case "WinY": if (TryInt(v, out n)) s.WinY = n; break;
                        case "StartHidden": if (bool.TryParse(v, out b)) s.StartHidden = b; break;
                        case "Name": s.Name = v.Length > 24 ? v.Substring(0, 24) : v; break;
                        case "Light": if (TryInt(v, out n)) s.Light = Math.Max(-1, Math.Min(3, n)); break;
                        case "LightColors": s.LightColors = v; break;
                        case "LightLevel": if (TryInt(v, out n)) s.LightLevel = Math.Max(0, Math.Min(100, n)); break;
                        case "LightEffect": if (TryInt(v, out n)) s.LightEffect = Math.Max(0, Math.Min(3, n)); break;
                        case "LightSpeed": if (TryInt(v, out n)) s.LightSpeed = Math.Max(1, Math.Min(5, n)); break;
                        case "ExternalEffect": if (TryInt(v, out n)) s.ExternalEffect = Math.Max(0, Math.Min(8, n)); break;
                        case "ExternalSpeed": if (TryInt(v, out n)) s.ExternalSpeed = Math.Max(1, Math.Min(5, n)); break;
                        case "ExternalLevel": if (TryInt(v, out n)) s.ExternalLevel = Math.Max(5, Math.Min(100, n)); break;
                        case "ExternalColor": if (!string.IsNullOrEmpty(v)) s.ExternalColor = v; break;
                        case "RefreshHz": if (TryInt(v, out n)) s.RefreshHz = Math.Max(0, Math.Min(500, n)); break;
                        case "LowHzOnBattery": if (bool.TryParse(v, out b)) s.LowHzOnBattery = b; break;
                        case "TrayTemp": if (bool.TryParse(v, out b)) s.TrayTemp = b; break;
                        case "Guard": if (bool.TryParse(v, out b)) s.Guard = b; break;
                        case "GuardCpu": if (int.TryParse(v, out n) && n >= 70 && n <= 105) s.GuardCpu = n; break;
                        case "GuardChassis": if (int.TryParse(v, out n) && n >= 40 && n <= 80) s.GuardChassis = n; break;
                        case "GuardLevel": if (int.TryParse(v, out n) && n >= 0 && n <= 255) s.GuardLevel = n; break;
                        case "GuardHold": if (int.TryParse(v, out n) && n >= 0 && n <= 900) s.GuardHold = n; break;
                        case "UpdateOnLaunch": if (bool.TryParse(v, out b)) s.UpdateOnLaunch = b; break;
                        case "MemCleanWorkingSet": if (bool.TryParse(v, out b)) s.MemCleanWorkingSet = b; break;
                        case "MemCleanSystemCache": if (bool.TryParse(v, out b)) s.MemCleanSystemCache = b; break;
                        case "MemCleanStandby": if (bool.TryParse(v, out b)) s.MemCleanStandby = b; break;
                        case "MemCleanModified": if (bool.TryParse(v, out b)) s.MemCleanModified = b; break;
                        case "MemCleanCombined": if (bool.TryParse(v, out b)) s.MemCleanCombined = b; break;
                        case "MemCleanRegistry": if (bool.TryParse(v, out b)) s.MemCleanRegistry = b; break;
                        case "MemCleanIntervalMin": if (TryInt(v, out n)) s.MemCleanIntervalMin = Math.Max(0, Math.Min(1440, n)); break;
                        case "MemCleanThresholdPct": if (TryInt(v, out n)) s.MemCleanThresholdPct = Math.Max(0, Math.Min(95, n)); break;
                        case "MemCleanNotify": if (bool.TryParse(v, out b)) s.MemCleanNotify = b; break;
                        case "KbdLighting": if (bool.TryParse(v, out b)) s.KbdLighting = b; break;
                        case "Overlay": if (bool.TryParse(v, out b)) s.Overlay = b; break;
                        case "OverlayPinned": if (bool.TryParse(v, out b)) s.OverlayPinned = b; break;
                        case "OverlayHorizontal": if (bool.TryParse(v, out b)) s.OverlayHorizontal = b; break;
                        case "OverlayShowCpu": if (bool.TryParse(v, out b)) s.OverlayShowCpu = b; break;
                        case "OverlayShowGpu": if (bool.TryParse(v, out b)) s.OverlayShowGpu = b; break;
                        case "OverlayShowRam": if (bool.TryParse(v, out b)) s.OverlayShowRam = b; break;
                        case "OverlayShowFps": if (bool.TryParse(v, out b)) s.OverlayShowFps = b; break;
                        case "OverlayShowUpload": if (bool.TryParse(v, out b)) s.OverlayShowUpload = b; break;
                        case "OverlayShowDownload": if (bool.TryParse(v, out b)) s.OverlayShowDownload = b; break;
                        case "OverlayShowPerf": if (bool.TryParse(v, out b)) s.OverlayShowPerf = b; break;
                        case "OverlayX": { double d; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.OverlayX = d; break; }
                        case "OverlayY": { double d; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.OverlayY = d; break; }
                        case "OverlayOpacity": if (TryInt(v, out n)) s.OverlayOpacity = Math.Max(20, Math.Min(100, n)); break;
                        case "OverlayPerfMode": if (TryInt(v, out n)) s.OverlayPerfMode = Math.Max(0, Math.Min(3, n)); break;
                        case "OverlayPerfPinned": if (bool.TryParse(v, out b)) s.OverlayPerfPinned = b; break;
                        case "OverlayPerfX": { double d; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.OverlayPerfX = d; break; }
                        case "OverlayHotkey": if (!string.IsNullOrEmpty(v)) s.OverlayHotkey = v; break;
                        case "OverlayDock": if (bool.TryParse(v, out b)) s.OverlayDock = b; break;
                        case "OverlayDockX": { double d; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.OverlayDockX = d; break; }
                        case "OverlayDockY": { double d; if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.OverlayDockY = d; break; }
                        case "KeyCommand": s.KeyCommand = v; break;
                        case "DriverUse": if (bool.TryParse(v, out b)) s.DriverUse = b; break;
                        case "DriverInstalledBySeal": case "DriverInstalledByOhman": if (bool.TryParse(v, out b)) s.DriverInstalledBySeal = b; break;
                        case "DriverRestartPending": if (bool.TryParse(v, out b)) s.DriverRestartPending = b; break;
                        case "DriverNudgeDismissed": s.DriverNudgeDismissed = v.Length > 24 ? v.Substring(0, 24) : v; break;
                        case "FanCeilingSeen": { int fc; if (int.TryParse(v, out fc) && fc >= 0 && fc <= 255) s.FanCeilingSeen = fc; break; }
                        case "MaxIgnored": s.MaxIgnored = v == "1"; break;
                    }
                }
            } catch (Exception ex) { Log.Write("settings apply " + k + ": " + ex.Message); }
        }

        internal static bool TryInt(string v, out int n) {
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return int.TryParse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n);
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
        }

        static readonly object saveSync = new object();


        public void Delete() {
            NoPersist = true;
            try { if (System.IO.File.Exists(File_)) System.IO.File.Delete(File_); } catch { }
        }

        public void Save() {
            if (NoPersist) return;
            try {
                lock (saveSync) { WriteFile(); }
            } catch (Exception ex) { Log.Write("save settings: " + ex.Message); }
        }
        void WriteFile() {
            {
                var sb = new StringBuilder();
                sb.AppendLine("# " + Program.AppName + " settings (edited by the app; safe to hand-edit while it is closed)");
                sb.AppendLine("Ver=" + FileVer);
                sb.AppendLine("ModeIndex=" + (SavedModeOverride >= 0 ? SavedModeOverride : ModeIndex));
                sb.AppendLine("EcoCool=" + EcoCool);
                sb.AppendLine("# per-mode profiles: M0 = Eco, M1 = Balanced, M2 = Performance");
                for (int i = 0; i < 3; i++) Modes[i].Write(sb, "M" + i + ".");
                sb.AppendLine("Key=" + (int)Key);
                sb.AppendLine("KeyId=" + KeyId);
                sb.AppendLine("KeyData=" + KeyData);
                sb.AppendLine("SuppressOgh=" + SuppressOgh);
                sb.AppendLine("Hotkeys=" + Hotkeys);
                sb.AppendLine("EcoOnBattery=" + EcoOnBattery);
                sb.AppendLine("SyncWinPower=" + SyncWinPower);
                sb.AppendLine("HeartbeatSec=" + HeartbeatSec);
                sb.AppendLine("Light=" + Light);
                sb.AppendLine("LightColors=" + LightColors);
                sb.AppendLine("LightLevel=" + LightLevel);
                sb.AppendLine("LightEffect=" + LightEffect);
                sb.AppendLine("LightSpeed=" + LightSpeed);
                sb.AppendLine("ExternalEffect=" + ExternalEffect);
                sb.AppendLine("ExternalSpeed=" + ExternalSpeed);
                sb.AppendLine("ExternalLevel=" + ExternalLevel);
                sb.AppendLine("ExternalColor=" + ExternalColor);
                sb.AppendLine("RefreshHz=" + RefreshHz);
                sb.AppendLine("LowHzOnBattery=" + LowHzOnBattery);
                sb.AppendLine("TrayTemp=" + TrayTemp);
                sb.AppendLine("KeyCommand=" + KeyCommand);
                for (int i = 0; i < HotkeyTable.Count; i++) if (HotkeyText[i] != null) sb.AppendLine("Hotkey." + HotkeyTable.Keys[i] + "=" + HotkeyText[i]);
                sb.AppendLine("Guard=" + Guard);
                if (GuardCpu > 0) sb.AppendLine("GuardCpu=" + GuardCpu);
                if (GuardChassis > 0) sb.AppendLine("GuardChassis=" + GuardChassis);
                if (GuardLevel > 0) sb.AppendLine("GuardLevel=" + GuardLevel);
                if (GuardHold > 0) sb.AppendLine("GuardHold=" + GuardHold);
                sb.AppendLine("UpdateOnLaunch=" + UpdateOnLaunch);
                sb.AppendLine("MemCleanWorkingSet=" + MemCleanWorkingSet);
                sb.AppendLine("MemCleanSystemCache=" + MemCleanSystemCache);
                sb.AppendLine("MemCleanStandby=" + MemCleanStandby);
                sb.AppendLine("MemCleanModified=" + MemCleanModified);
                sb.AppendLine("MemCleanCombined=" + MemCleanCombined);
                sb.AppendLine("MemCleanRegistry=" + MemCleanRegistry);
                sb.AppendLine("MemCleanIntervalMin=" + MemCleanIntervalMin);
                sb.AppendLine("MemCleanThresholdPct=" + MemCleanThresholdPct);
                sb.AppendLine("MemCleanNotify=" + MemCleanNotify);
                sb.AppendLine("KbdLighting=" + KbdLighting);
                sb.AppendLine("Overlay=" + Overlay);
                sb.AppendLine("OverlayPinned=" + OverlayPinned);
                sb.AppendLine("OverlayHorizontal=" + OverlayHorizontal);
                sb.AppendLine("OverlayShowCpu=" + OverlayShowCpu);
                sb.AppendLine("OverlayShowGpu=" + OverlayShowGpu);
                sb.AppendLine("OverlayShowRam=" + OverlayShowRam);
                sb.AppendLine("OverlayShowFps=" + OverlayShowFps);
                sb.AppendLine("OverlayShowUpload=" + OverlayShowUpload);
                sb.AppendLine("OverlayShowDownload=" + OverlayShowDownload);
                sb.AppendLine("OverlayShowPerf=" + OverlayShowPerf);
                if (OverlayX >= 0) sb.AppendLine("OverlayX=" + OverlayX.ToString("0.0", CultureInfo.InvariantCulture));
                if (OverlayY >= 0) sb.AppendLine("OverlayY=" + OverlayY.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine("OverlayOpacity=" + OverlayOpacity);
                sb.AppendLine("OverlayPerfMode=" + OverlayPerfMode);
                sb.AppendLine("OverlayPerfPinned=" + OverlayPerfPinned);
                if (OverlayPerfX >= 0) sb.AppendLine("OverlayPerfX=" + OverlayPerfX.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine("OverlayHotkey=" + OverlayHotkey);
                sb.AppendLine("OverlayDock=" + OverlayDock);
                if (OverlayDockX >= 0) sb.AppendLine("OverlayDockX=" + OverlayDockX.ToString("0.0", CultureInfo.InvariantCulture));
                if (OverlayDockY >= 0) sb.AppendLine("OverlayDockY=" + OverlayDockY.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine("DriverUse=" + DriverUse);
                sb.AppendLine("DriverInstalledBySeal=" + DriverInstalledBySeal);
                sb.AppendLine("DriverRestartPending=" + DriverRestartPending);
                sb.AppendLine("DriverNudgeDismissed=" + DriverNudgeDismissed);
                sb.AppendLine("FanCeilingSeen=" + FanCeilingSeen);
                if (MaxIgnored) sb.AppendLine("MaxIgnored=1");
                sb.AppendLine("MaxBackWhenCool=" + MaxBackWhenCool);
                sb.AppendLine("MaxStopAfterMin=" + MaxStopAfterMin);
                sb.AppendLine("ManualLinked=" + ManualLinked);
                sb.AppendLine("UpdateChecked=" + UpdateChecked);
                sb.AppendLine("LatestVersion=" + LatestVersion);
                sb.AppendLine("CheckedFrom=" + CheckedFrom);
                sb.AppendLine("PollMs=" + PollMs);
                sb.AppendLine("TookWinLighting=" + TookWinLighting);
                sb.AppendLine("PerKeyReset=" + PerKeyReset);
                sb.AppendLine("FanBeforeMax=" + FanBeforeMax);
                sb.AppendLine("WinX=" + WinX);
                sb.AppendLine("WinY=" + WinY);
                sb.AppendLine("StartHidden=" + StartHidden);
                sb.AppendLine("# Name=   (optional: a different display name for the window and tray; no rebuild needed)");
                if (!string.IsNullOrEmpty(Name)) sb.AppendLine("Name=" + Name);
                File.WriteAllText(File_, sb.ToString());
            }
        }
    }

    public sealed class Engine : IDisposable {
        public readonly IHardware Hw;
        public readonly Settings S;
        public SystemInfo Info = new SystemInfo();
        public int FanCount = -1;
        public bool BiosOk;
        public string LastError = "";
        public DateTime LastHeartbeat = DateTime.MinValue;
        public volatile bool Learning;
        public uint LastEventId, LastEventData;
        public DateTime LastEventTime = DateTime.MinValue;

        public event Action StateChanged;
        public event Action<string, bool> Toast;
        public event Action<KeyAction> KeyPressed;
        public event Action<uint, uint> AnyKeyEvent;
        public event Action<Rgb[]> FrameChanged;

        public static readonly string[] ModeNames = { "Eco", "Balanced", "Performance" };
        public byte[] ModeBytes { get { return new byte[] { S.EcoCool ? P.ModeCool : P.ModeEco, P.ModeBalanced, P.ModePerformance }; } }
        public bool OnBattery;


        public PlatformProfile P = new PlatformProfile { Name = "(detecting)", Boards = new string[0] };
        public string Board = "", Model = "";
        public bool Supported;
        public bool Generic;
        public int GpuMode = -1;
        public int GpuModePending = -1;
        public static readonly string[] GpuModeNames = { "Hybrid", "Discrete", "Optimus", "iGPU only" };

        bool graphicsReadable;
        public bool GpuModeOffered(int mode) {
            if (Hw.IsDemo) {
                int bit = mode == 3 ? 1 : mode == 0 ? 2 : mode == 1 ? 4 : 8;
                return (Info.GpuModes & bit) != 0;
            }
            if (mode == 0 || mode == 3) return true;
            if (mode == 1 && (Info.GpuModes & 4) != 0) return true;
            return false;
        }
        public ILighting Light;
        public Rgb[] LightColors = new Rgb[0];
        public bool ReadOnly { get { return !Supported && !Hw.IsDemo; } }

        public CpuRegisters Cpu;
        public EmbeddedController Ec;
        public string DriverWhy = "";
        public volatile bool DriverBusy;
        public volatile string DriverProgress = "";
        public enum FanRoute { Mailbox, Ec }
        public FanRoute Route = FanRoute.Mailbox;
        bool ecVerified;
        public string EcProof = "";
        public bool EcVerified { get { return ecVerified; } }
        public bool DriverReady { get { return Cpu != null || (Ec != null && ecVerified); } }

        public Version DriverVersion;
        public bool DriverInstalled { get { return Hw.IsDemo ? DriverReady : DriverVersion != null; } }
        public bool DriverOutdated { get { return DriverVersion != null && DriverVersion < PawnIo.MinVersion; } }

        ManagementEventWatcher watcher;
        System.Threading.Timer heartbeat;
        DateTime lastKey = DateTime.MinValue;
        readonly object applySync = new object();
        bool ecoForcedByBattery;
        int modeBeforeBattery = 1;

        public Engine(IHardware hw, Settings s) { Hw = hw; S = s; }

        public int ModeIndex { get { return Math.Max(0, Math.Min(2, S.ModeIndex)); } }
        public byte ModeByte { get { return ModeBytes[ModeIndex]; } }
        public string ModeName { get { return ModeNames[ModeIndex]; } }
        public int BaseTdp { get { return Info.Valid && Info.DefaultConcurrentTdp > 0 ? Info.DefaultConcurrentTdp : P.TdpBase; } }
        public int MaxOffset { get { return P.TdpGainMax; } }
        public int CurrentTdp { get { return BaseTdp + Math.Max(0, Math.Min(MaxOffset, S.TdpOffset)); } }
        public uint KeyId { get { return S.KeyId != 0 ? S.KeyId : P.KeyEventId; } }
        public uint KeyData { get { return S.KeyId != 0 ? S.KeyData : P.KeyEventData; } }
        public GpuLevel EffectiveGpu { get { return S.GpuAuto ? GpuForMode(ModeIndex) : S.Gpu; } }
        public static GpuLevel GpuForMode(int modeIndex) { return modeIndex == 0 ? GpuLevel.Base : modeIndex == 1 ? GpuLevel.Boost : GpuLevel.Max; }

        public long CleanMemory(out int procCount) {
            MemoryAreas areas = MemoryAreas.None;
            if (S.MemCleanWorkingSet) areas |= MemoryAreas.WorkingSet;
            if (S.MemCleanSystemCache) areas |= MemoryAreas.SystemFileCache;
            if (S.MemCleanStandby) areas |= MemoryAreas.StandbyList;
            if (S.MemCleanModified) areas |= MemoryAreas.ModifiedPageList;
            if (S.MemCleanCombined) areas |= MemoryAreas.CombinedPageList;
            if (S.MemCleanRegistry) areas |= MemoryAreas.RegistryCache;
            if (areas == MemoryAreas.None) areas = MemoryAreas.All;
            return MemoryCleaner.Optimize(areas, out procCount);
        }


        public DateTime LastUpdateCheck { get { return S.UpdateChecked == 0 ? DateTime.MinValue : new DateTime(S.UpdateChecked); } }
        public string LatestVersion { get { return S.LatestVersion; } }
        public bool UpdateAvailable { get { return Update.Newer(S.LatestVersion, Program.Version); } }

        public void CheckForUpdate(bool force) {

            bool otherBuild = S.CheckedFrom != Program.Version;
            if (!force && !otherBuild && S.UpdateChecked != 0 && (DateTime.Now - LastUpdateCheck).TotalHours < 24) return;
            Release rel = Update.Latest();
            string tag = rel == null ? null : rel.Tag;
            S.UpdateChecked = DateTime.Now.Ticks;


            if (tag != null) { S.LatestVersion = tag; S.CheckedFrom = Program.Version; }
            S.Save();
            if (force) Say(tag == null ? "Update check failed" : Update.Newer(tag, Program.Version) ? "Version " + tag + " is available" : "Seal is up to date");
            Changed();

            string have = Staged ?? Program.Version;
            if (rel != null && S.UpdateOnLaunch && Update.Newer(rel.Tag, have) && Update.Stage(rel)) { RefreshStaged(); Changed(); }
        }
        volatile string staged;


        public string Staged { get { return staged; } }
        public void RefreshStaged() { staged = Update.Staged(); }

        public bool StartUpdate(out string error) { return Update.Swap(out error); }

        public void Init() { Init(true); }

        public void Init(bool apply) {
            RefreshStaged();
            Board = Platforms.ReadBoard();
            Model = Platforms.ReadModel();
            var vendor = Platforms.ReadVendor();
            var prof = Platforms.Find(Board);
            if (prof == null) {
                if (vendor == LaptopVendor.Asus) prof = Platforms.GenericAsus(Board, Model);
                else if (vendor == LaptopVendor.Acer) prof = Platforms.GenericAcer(Board, Model);
            }
            Supported = prof != null || Hw.IsDemo;
            if (prof != null) P = prof;
            Log.Write("platform: vendor='" + vendor + "' model='" + Model + "' board='" + Board + "' -> " + (prof != null ? prof.Name : "no verified profile"));
            try {

                try { FanCount = Hw.GetFanCountPassive(); }
                catch (Exception ex) { FanCount = -1; Log.Write("no fan table (" + ex.Message + "): fan readout unavailable"); }


                try { Info = Hw.GetSystemInfo(); }
                catch (Exception ex) { Info = new SystemInfo(); Log.Write("system data (0x28) unavailable: " + ex.Message); }
                BiosOk = true;
                if (!Info.Valid) Log.Write("no system data: power gain and graphics cannot be offered on this board");
                if (Supported && !Hw.IsDemo && Info.Valid && Info.ThermalPolicy != P.ThermalPolicy) {
                    Supported = false;
                    Log.Write("thermal policy v" + Info.ThermalPolicy + " does not match the profile (v" + P.ThermalPolicy + "); switching to read-only");
                }
                if (prof == null && (!Hw.IsDemo || Platforms.BoardOverride != null)) {

                    var g = Platforms.Generic(Board, Info);
                    if (g != null) {
                        try { Hw.GetGpuPower(); g.HasGpuPower = true; } catch (Exception ex) { Log.Write("generic: no GPU power control (" + ex.Message + ")"); }
                        try { int top = Hw.GetFanTableMax(); if (top > g.Curve.Ceiling) g.Curve.Rescale(top); } catch { }
                        P = g;
                        Supported = true;
                        Generic = true;
                        Log.Write("generic profile: " + g.Notes + " · modes " + g.ModeEco.ToString("X2") + "/" + g.ModeBalanced.ToString("X2") + "/" + g.ModePerformance.ToString("X2") + " · powerGain=" + g.HasPowerGain + " (base " + g.TdpBase + " W) · gpuPower=" + g.HasGpuPower + " · fan ceiling " + g.Curve.Ceiling);
                    } else Log.Write("generic profile not possible (thermal policy v" + Info.ThermalPolicy + "); read-only");
                }
                ApplyMeasuredCeiling();

                try {
                    GpuMode = Hw.GetGpuMode();
                    if (GpuMode < 0 || GpuMode > 3) GpuMode = 0;
                    graphicsReadable = true;
                }
                catch (Exception ex) {
                    GpuMode = 0;
                    graphicsReadable = true;
                    Log.Write("graphics mode read: " + ex.Message + " - defaulting to Hybrid");
                }
                Log.Write("BIOS ok: fans=" + FanCount + " policy=v" + Info.ThermalPolicy + " swFan=" + Info.SwFanControl + " defPL4=" + Info.DefaultPl4 + "W baseTdp=" + Info.DefaultConcurrentTdp + "W raw=" + Info.Hex + (Hw.IsDemo ? " (DEMO)" : ""));
            } catch (Exception ex) { BiosOk = false; LastError = ex.Message; Log.Write("BIOS self-test FAILED: " + ex.Message); }
            InitDriver();


            if (!apply) { InitLight(); Log.Write("probe only: nothing was applied"); return; }

            if (S.SuppressOgh && !Hw.IsDemo && Supported) { KillOgh(); new Thread(delegate() { SetOghTasks(true); }) { IsBackground = true }.Start(); }
            if (S.EcoOnBattery && OnBattery && ModeIndex != 0) { ecoForcedByBattery = true; modeBeforeBattery = ModeIndex; S.SavedModeOverride = modeBeforeBattery; S.ModeIndex = 0; Log.Write("on battery at start: Eco (user mode " + ModeNames[modeBeforeBattery] + " kept)"); }

            foreach (var m in S.Modes) {
                if (m.CurveLevels == null) m.CurveLevels = VendorCurveAt(false);
                if (m.GpuCurveLevels == null) m.GpuCurveLevels = VendorCurveAt(true);
            }
            InitLight();
            ApplyRefreshRate(OnBattery);
            NoteFanMode(S.Fan);
            ApplyAll(false);
            StartKeyWatcher();
            heartbeat = new System.Threading.Timer(delegate { Heartbeat(); }, null, S.HeartbeatSec * 1000, S.HeartbeatSec * 1000);
            guard = new System.Threading.Timer(delegate { GuardTick(); }, null, 10000, 10000);
            fanTimer = new System.Threading.Timer(delegate { FanTick(); }, null, 5000, 5000);
        }

        System.Threading.Timer fanTimer;

        DateTime maxSince = DateTime.MinValue, maxCoolSince = DateTime.MinValue;
        string maxStopReason = "";
        public int MaxMinutes { get { return maxSince == DateTime.MinValue ? 0 : (int)(DateTime.Now - maxSince).TotalMinutes; } }
        public TimeSpan MaxLeft {
            get {
                if (S.MaxStopAfterMin <= 0 || maxSince == DateTime.MinValue) return TimeSpan.Zero;
                var left = TimeSpan.FromMinutes(S.MaxStopAfterMin) - (DateTime.Now - maxSince);
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }


        void NoteFanMode(FanMode mode) {
            if (mode == FanMode.Max) { if (maxSince == DateTime.MinValue) { maxSince = DateTime.Now; maxCoolSince = DateTime.MinValue; } }
            else { maxSince = DateTime.MinValue; maxCoolSince = DateTime.MinValue; }
        }

        bool MaxShouldStop() {
            if (maxSince == DateTime.MinValue) return false;
            if (S.MaxStopAfterMin > 0 && (DateTime.Now - maxSince).TotalMinutes >= S.MaxStopAfterMin) { maxStopReason = S.MaxStopAfterMin + " min elapsed"; return true; }
            if (!S.MaxBackWhenCool) { maxCoolSince = DateTime.MinValue; return false; }
            double t = double.IsNaN(CpuTemp) ? GpuTemp : double.IsNaN(GpuTemp) ? CpuTemp : Math.Max(CpuTemp, GpuTemp);
            if (double.IsNaN(t) || t >= P.Guard.MaxFanCoolBelow) { maxCoolSince = DateTime.MinValue; return false; }
            if (maxCoolSince == DateTime.MinValue) { maxCoolSince = DateTime.Now; return false; }
            if ((DateTime.Now - maxCoolSince).TotalSeconds < P.Guard.MaxFanCoolSeconds) return false;
            maxStopReason = "below " + P.Guard.MaxFanCoolBelow + "° for " + (P.Guard.MaxFanCoolSeconds / 60) + " minutes";
            return true;
        }
        void FanTick() {
            if (!BiosOk && !Hw.IsDemo) return;
            bool leaveMax = false;
            try {
                lock (applySync) {
                    if (GuardActive) return;
                    switch (S.Fan) {
                        case FanMode.Auto: case FanMode.Custom: AutoTick(false); break;
                        case FanMode.Manual: if ((DateTime.Now - lastFanWrite).TotalSeconds >= 30) { WriteLevels(S.Fan1, S.Fan2, "Fan level"); lastFanWrite = DateTime.Now; } break;
                        case FanMode.Max:
                            if ((DateTime.Now - lastFanWrite).TotalSeconds >= 30) { MaxFan(true, "Max fan"); lastFanWrite = DateTime.Now; }
                            if (MaxShouldStop()) { Log.Write("max fan: " + maxStopReason + ", back to auto"); leaveMax = true; }
                            break;
                    }
                }
            } catch (Exception ex) { Log.Write("fan tick: " + ex.Message); }
            if (leaveMax) { Say("Max fan off · " + maxStopReason); SetFan(fanBeforeMax, S.Fan1, S.Fan2, false); }
        }

        readonly Queue<Action> work = new Queue<Action>();
        readonly AutoResetEvent workReady = new AutoResetEvent(false);
        Thread worker;
        volatile bool stopping;
        public void Post(Action a) {
            if (a == null) return;
            Thread start = null;
            lock (work) {
                work.Enqueue(a);
                if (worker == null) { worker = new Thread(WorkLoop) { IsBackground = true, Name = "seal-work" }; start = worker; }
            }
            if (start != null) start.Start();
            workReady.Set();
        }
        void WorkLoop() {
            while (!stopping) {
                workReady.WaitOne(500);
                for (; ; ) {
                    Action a;
                    lock (work) { if (work.Count == 0) break; a = work.Dequeue(); }
                    try { a(); } catch (Exception ex) { Log.Write("work: " + ex); }
                }
            }
        }

        public string FactoryReset() {
            var done = new List<string>();
            try { SetOghTasks(false); done.Add("re-enabled OMEN Gaming Hub's tasks"); }
            catch (Exception ex) { Log.Write("reset ogh tasks: " + ex.Message); }
            try {
                if (!Hw.IsDemo && S.TookWinLighting) {
                    WinLighting.SetControl(true);
                    S.TookWinLighting = false;
                    done.Add("gave the keyboard back to Windows Dynamic Lighting");
                }
            } catch (Exception ex) { Log.Write("reset lighting: " + ex.Message); }
            try {
                int top = Display.HighestHz();
                if (top > 0 && (S.RefreshHz > 0 || S.LowHzOnBattery) && Display.CurrentHz() != top) {
                    Display.SetHz(top);
                    done.Add("put the refresh rate back to " + top + " Hz");
                }
            } catch (Exception ex) { Log.Write("reset refresh: " + ex.Message); }
            if (BiosOk && !Hw.IsDemo && !ReadOnly) {
                try {
                    lock (applySync) {
                        Try(delegate { Hw.SetMode(P.ModeBalanced, true); }, "Reset mode");
                        exitMode = P.ModeBalanced;
                        MaxFan(false, "Reset max fan");
                        if (Route == FanRoute.Ec) ReleaseEcFans("reset");
                        else WriteLevels(P.Curve.Fallback, P.Curve.Fallback, "Reset fan level");
                    }
                    done.Add("set the mode back to balanced and handed the fans back");
                } catch (Exception ex) { Log.Write("reset firmware: " + ex.Message); }
            }
            try {
                if (Light != null && !Hw.IsDemo) {
                    TryLight(delegate { Light.SetBacklight(true, 100); }, "Reset backlight");
                    done.Add("turned the keyboard backlight back on");
                }
            } catch (Exception ex) { Log.Write("reset backlight: " + ex.Message); }
            ReleasePerKey();
            var sb = new StringBuilder();
            foreach (string d in done) sb.AppendLine("  - " + d);
            Log.Write("factory reset: " + string.Join("; ", done.ToArray()));
            return sb.ToString();
        }

        public void Park() { Park(false); }

        public void Park(bool quiet) {

            try {
                if (!Hw.IsDemo && S.TookWinLighting) {
                    WinLighting.SetControl(true);
                    S.TookWinLighting = false;
                    S.Save();
                    Log.Write("handed the keyboard back to Windows Dynamic Lighting");
                }
            } catch (Exception ex) { Log.Write("release lighting: " + ex.Message); }
            ReleasePerKey();
            if (!BiosOk || Hw.IsDemo || ReadOnly) return;
            try {

                MaxFan(false, "Max fan off on exit");

                int cur = Math.Max(curLevel1, curLevel2);
                int want = quiet && cur >= 0 ? cur : Math.Max(P.Curve.Fallback, cur);


                if (Route == FanRoute.Ec) { lock (applySync) ReleaseEcFans("exit"); }
                else {
                    lock (applySync) WriteLevels(want, want, "Fan level on exit");
                    Log.Write("parked fans at " + want + " and cleared max fan");
                }

                byte m = exitMode >= 0 ? (byte)exitMode : ModeByte;
                bool handed;
                lock (applySync) handed = Try(delegate { Hw.SetMode(m, true); }, "Fans to the firmware on exit");
                if (handed) Log.Write("fans handed to the firmware's own curve (mode 0x" + m.ToString("X2") + ", fan control by BIOS)");
            } catch (Exception ex) { Log.Write("park fans: " + ex.Message); }
        }

        public void Dispose() {
            stopping = true; try { workReady.Set(); } catch { }
            try { if (fx != null) fx.Dispose(); } catch { }
            CloseDriver();
            try { if (fanTimer != null) fanTimer.Dispose(); } catch { }
            try { if (heartbeat != null) heartbeat.Dispose(); } catch { }
            try { if (guard != null) guard.Dispose(); } catch { }
            try { if (watcher != null) { watcher.Stop(); watcher.Dispose(); } } catch { }
        }

        bool TryLight(Action a, string what) {
            try { a(); return true; }
            catch (Exception ex) { Log.Write("FAIL " + what + ": " + ex.Message); Fire(Toast, what + " failed: " + ex.Message, true); return false; }
        }

        bool Try(Action a, string what) {
            if (ReadOnly) { Log.Write("read-only (unsupported board '" + Board + "'): skipped " + what); return false; }
            try { a(); LastError = ""; return true; }
            catch (Exception ex) { LastError = ex.Message; Log.Write("FAIL " + what + ": " + ex.Message); Fire(Toast, what + " failed: " + ex.Message, true); return false; }
        }
        void Fire(Action<string, bool> h, string m, bool err) { if (h != null) { try { h(m, err); } catch { } } }
        void Changed() { var h = StateChanged; if (h != null) { try { h(); } catch { } } }

        void Say(string m) { Log.Write(m); Fire(Toast, m, false); }

        public void ApplyAll(bool announce) {
            lock (applySync) {
                if (!BiosOk && !Hw.IsDemo) return;
                Try(delegate { Hw.SetMode(ModeByte, FansByBios); }, "Set mode");
                ApplyFanCore();
                ApplyPowerCore();
                ApplyGpuCore();
                if (S.SyncWinPower) SetWinPowerOverlay(ModeIndex);
                LastHeartbeat = DateTime.Now;
                ApplyLightCore();
            }
            if (announce) Say("Applied " + ModeName + " · +" + S.TdpOffset + " W");
            Changed();
        }

        int curLevel1 = -1, curLevel2 = -1;
        int exitMode = -1;

        int pristineCeiling;
        void ApplyMeasuredCeiling() {
            if (P == null || P.Curve == null) return;

            if (pristineCeiling == 0) pristineCeiling = P.Curve.Ceiling;
            int want = S.FanCeilingSeen > P.Curve.Floor + CeilingUsableRange ? S.FanCeilingSeen : pristineCeiling;
            int was = P.Curve.Ceiling;
            if (want == was) return;
            if (Generic) P.Curve.Rescale(want); else P.Curve.Ceiling = want;
            Log.Write("fan ceiling " + was + " -> " + P.Curve.Ceiling
                + (want == pristineCeiling ? ", back to the profile's own" : ", measured on this machine")
                + (Generic ? " (curve rescaled with it)" : ""));
        }

        int ceilingTicks, ceilingHighWater;
        const int CeilingSettleTicks = 8;

        const int CeilingOver = 2, CeilingShort = 10;

        const int CeilingUsableRange = 15;
        readonly object ceilingSync = new object();

        public void NoteFanLevels(int[] f) {
            if (f == null || f.Length < 2 || P == null || P.Curve == null) return;


            lock (ceilingSync) NoteFanLevelsCore(f);
        }
        void NoteFanLevelsCore(int[] f) {
            int seen = Math.Max(f[0], f[1]);
            if (seen < 0 || seen > 255) return;
            lastFanSeen = seen;
            if (maxAskedAt != DateTime.MinValue && (DateTime.Now - maxAskedAt).TotalSeconds >= 15) {
                maxAskedAt = DateTime.MinValue;


                if ((maxAskedFrom >= 0 && seen >= maxAskedFrom + 3) || seen >= P.Curve.Ceiling - CeilingShort) { maxProven = true; maxIgnoredVerdicts = 0; }
                else if (maxAskedFrom > 0 && CanSetFanLevels && ++maxIgnoredVerdicts >= 2) {

                    S.MaxIgnored = true;
                    S.Save();
                    Log.Write("max fan: the firmware took the command twice and the fans stayed at " + seen + " (from " + maxAskedFrom
                        + "); Max and the thermal guard write the ceiling as a level from now on");
                    lastFanWrite = DateTime.MinValue;
                }
            }
            if (seen == 0) return;


            bool maxAsking = (GuardActive || S.Fan == FanMode.Max) && maxProven;
            if (!(maxAsking || Math.Max(curLevel1, curLevel2) >= P.Curve.Ceiling)) {
                ceilingTicks = 0; ceilingHighWater = 0; return;
            }
            if (seen > ceilingHighWater) ceilingHighWater = seen;
            if (++ceilingTicks < CeilingSettleTicks) return;
            ceilingTicks = 0;
            int real = ceilingHighWater;
            int gap = real - P.Curve.Ceiling;
            if (gap < CeilingOver && gap > -CeilingShort) return;
            if (real <= P.Curve.Floor + CeilingUsableRange || real == S.FanCeilingSeen) return;
            S.FanCeilingSeen = real;
            S.Save();
            Log.Write("fan ceiling learned: asked for " + P.Curve.Ceiling + " and these fans never went past " + real
                + "; using " + real + " from the next start");
        }
        public int AutoLevel1 { get { return curLevel1; } }
        public int AutoLevel2 { get { return curLevel2; } }
        public double GpuTemp = double.NaN, IrTemp = double.NaN;
        int fanWriteFailures;

        public bool CanSetFanLevels { get { return !fanLevelsRefused || Route == FanRoute.Ec; } }
        const int FanWriteGiveUp = 8;
        bool fanLevelsRefused;

        bool WriteLevels(int l1, int l2, string what) {

            l1 = P.Curve.ClampOrOff(l1);
            l2 = P.Curve.ClampOrOff(l2);
            if (Route == FanRoute.Ec) return WriteLevelsEc(l1, l2, what);

            if (fanLevelsRefused && Ec != null && ChooseRoute() == FanRoute.Ec) return WriteLevelsEc(l1, l2, what);
            if (fanLevelsRefused) return false;
            if (Try(delegate { Hw.GetFanCount(); Hw.SetFanLevels(l1, l2); }, what)) { curLevel1 = l1; curLevel2 = l2; fanWriteFailures = 0; fanFailureShown = false; return true; }
            fanWriteFailures++;
            if (fanWriteFailures >= FanWriteGiveUp) {
                fanLevelsRefused = true;
                Log.Write("giving up on fan levels after " + fanWriteFailures + " refusals; this firmware will not take them. Max fan and the modes are unaffected.");


                if (ChooseRoute() == FanRoute.Ec) { Say("Fan levels now go through the driver"); return WriteLevelsEc(l1, l2, what); }
                Fire(Toast, "This firmware will not take fan levels; its own curve stays in charge", true);
                Changed();
            } else if (fanWriteFailures >= 3 && !fanFailureShown) {
                fanFailureShown = true;
                Fire(Toast, "Fan writes failing; firmware curve will take over", true);
            }
            return false;
        }

        int ecFailures;
        bool WriteLevelsEc(int l1, int l2, string what) {
            if (Ec == null) { Route = FanRoute.Mailbox; return false; }
            if (Ec.HoldFans(l1, l2, P.Curve.Ceiling)) { curLevel1 = l1; curLevel2 = l2; ecFailures = 0; LastError = ""; return true; }
            ecFailures++;
            LastError = Ec.LastError;
            Log.Write("FAIL " + what + " via EC: " + Ec.LastError);
            if (ecFailures >= 3) {
                Route = FanRoute.Mailbox;
                Log.Write("EC fan route abandoned after " + ecFailures + " failures; back to the mailbox");
                Fire(Toast, "The driver could not hold the fans (" + Ec.LastError + "); firmware curve in charge", true);
                Changed();
            }
            return false;
        }
        void ReleaseEcFans(string why) {
            if (Ec == null) return;
            if (Ec.ReleaseFans()) Log.Write("EC fans released on " + why + "; the controller resumes its own curve");
            else Log.Write("EC fans NOT released on " + why + ": " + Ec.LastError);
            curLevel1 = curLevel2 = -1;
        }

        void InitDriver() {
            CloseDriver();
            DriverWhy = "";
            EcProof = "";
            DriverVersion = Hw.IsDemo ? null : PawnIo.InstalledVersion();
            if (Hw.IsDemo) {

                if (P.DriverFor != DriverFor.None) { DriverWhy = "not installed"; return; }
                Cpu = new DemoCpu();
                Ec = new EmbeddedController(new DemoEcPorts(), EcMap.Legacy());
                ecVerified = true;
                EcProof = "simulated";
                Route = ChooseRoute();
                return;
            }
            if (!S.DriverUse) { DriverWhy = "switched off"; return; }
            if (DriverVersion == null) { DriverWhy = "not installed"; return; }
            if (DriverOutdated) { DriverWhy = "PawnIO " + DriverVersion + " is older than " + PawnIo.MinVersion + "; update it"; return; }
            string why;
            bool deviceAbsent;
            Cpu = CpuRegisters.Open(out why, out deviceAbsent);
            if (Cpu == null) {
                string svc = PawnIo.ServiceState();
                DriverWhy = why ?? "unavailable";
                Log.Write("driver: CPU registers unavailable: " + DriverWhy + " · service " + svc);

                if (S.DriverRestartPending && svc != "stopped" && svc != "starting") { S.DriverRestartPending = false; S.Save(); }
                if (!S.DriverRestartPending && svc != "running") DriverWhy += " · the PawnIO service is " + svc;

                if (deviceAbsent) return;
            }
            else if (S.DriverRestartPending) { S.DriverRestartPending = false; S.Save(); }
            if (P.Ec != null) {
                PawnIoModule m = PawnIo.Open("LpcACPIEC", out why);
                if (m == null) Log.Write("driver: EC module unavailable: " + why);
                else {

                    var ec = new EmbeddedController(new PawnIoEcPorts(m), P.Ec);

                    try {
                        string pair = System.IO.File.Exists(EcPairPath) ? System.IO.File.ReadAllText(EcPairPath).Trim() : "";
                        if (pair == "percent") { P.Ec.UsePercent = true; Log.Write("driver: fan levels go to the percent pair (measured by the fan test)"); }
                        else if (pair == "rpm") P.Ec.UsePercent = false;
                    } catch { }
                    int[] rpm = null;
                    double die = double.NaN;
                    try { rpm = Hw.GetFanLevels(); } catch { }
                    try { if (Cpu != null) die = Cpu.Poll().DieTemp; } catch { }
                    ecVerified = ec.Verify(rpm, die, out EcProof);
                    Log.Write("driver: the EC map " + (ecVerified ? "fits this board: " : "does NOT fit this board: ") + EcProof);
                    Ec = ec;
                }
            }
            Route = ChooseRoute();
            Log.Write("driver: PawnIO " + DriverVersion + " · cpu=" + (Cpu != null ? Cpu.Describe : "unavailable") + " · ec=" + (Ec == null ? "no map for this board" : ecVerified ? P.Ec.Name : "map rejected") + " · fan route " + Route);
        }

        public static string EcPairPath { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Log.Path), "ecpair.txt"); } }

        public int EcPairFound;
        void CloseDriver() {
            try { if (Cpu != null) Cpu.Dispose(); } catch { }
            try { if (Ec != null) Ec.Dispose(); } catch { }
            Cpu = null; Ec = null;
            ecVerified = false;
            Route = FanRoute.Mailbox;
        }

        public bool EcFanRouteWanted { get { return (P.DriverFor & DriverFor.FanLevels) != 0 || fanLevelsRefused; } }

        FanRoute ChooseRoute() {
            bool need = EcFanRouteWanted;
            FanRoute was = Route;
            Route = (Ec != null && ecVerified && need && !Ec.Resting) ? FanRoute.Ec : FanRoute.Mailbox;


            if (Route == FanRoute.Ec && was != FanRoute.Ec) { fanWriteFailures = 0; fanFailureShown = false; ecFailures = 0; Log.Write("fan route: EC"); }
            return Route;
        }

        public string DriverNudge {
            get {
                if (DriverReady || !S.DriverUse || DriverBusy) return null;
                if (S.DriverNudgeDismissed == Program.Version) return null;
                if (S.DriverRestartPending) return "Restart Windows to finish installing the driver";
                bool fans = (P.DriverFor & DriverFor.FanLevels) != 0 || (fanLevelsRefused && P.Ec != null);
                return fans ? "Fan levels on this board need a driver" : null;
            }
        }
        public void DismissDriverNudge() { S.DriverNudgeDismissed = Program.Version; S.Save(); Changed(); }

        public void InstallDriver() {
            if (DriverBusy) return;
            DriverBusy = true;
            DriverProgress = "Starting…";
            if (!S.DriverUse) { S.DriverUse = true; S.Save(); }
            Changed();
            try {
                string error;
                DriverInstallResult r = PawnIo.Install(delegate(string step) { DriverProgress = step; Changed(); }, out error);
                switch (r) {
                    case DriverInstallResult.Installed:
                        S.DriverInstalledBySeal = true;
                        S.DriverRestartPending = false;
                        S.Save();
                        lock (applySync) {
                            FanRoute before = Route;
                            InitDriver();
                            if (Route != before) ApplyFanCore();
                        }
                        Say(DriverReady ? "Driver installed" : "Driver installed, but it could not be opened: " + DriverWhy);
                        break;
                    case DriverInstallResult.RestartNeeded:
                        S.DriverInstalledBySeal = true;
                        S.DriverRestartPending = true;
                        S.Save();
                        InitDriver();
                        DriverWhy = "installed · restart Windows to finish";
                        Say("Driver installed · restart Windows to finish");
                        break;
                    default:
                        DriverWhy = error ?? "install failed";
                        Fire(Toast, "Driver install failed: " + DriverWhy, true);
                        break;
                }
            } finally { DriverBusy = false; DriverProgress = ""; Changed(); }
        }

        public bool RemoveDriver() { return RemoveDriver(true); }

        public bool RemoveDriver(bool reapply) {
            if (DriverBusy) return false;
            DriverBusy = true;
            DriverProgress = "Removing…";
            Changed();
            try {
                lock (applySync) {
                    if (Route == FanRoute.Ec) ReleaseEcFans("driver removal");
                    CloseDriver();
                }
                string error;
                bool ok = PawnIo.Uninstall(out error);
                if (ok) { S.DriverInstalledBySeal = false; S.DriverRestartPending = false; S.Save(); }

                lock (applySync) InitDriver();
                if (ok) Say("Driver removed");
                else { DriverWhy = error; Fire(Toast, "Could not remove the driver: " + error, true); }
                if (reapply) lock (applySync) ApplyFanCore();
                return ok;
            } finally { DriverBusy = false; DriverProgress = ""; Changed(); }
        }

        public void SetDriverUse(bool on) {
            S.DriverUse = on;
            S.Save();
            lock (applySync) {
                if (Route == FanRoute.Ec && !on) ReleaseEcFans("driver switched off");
                InitDriver();
                ApplyFanCore();
            }
            Changed();
        }
        bool fanFailureShown;
        FanMode fanBeforeMax { get { return (FanMode)S.FanBeforeMax; } set { S.FanBeforeMax = (int)value; } }

        void MaxFan(bool on, string what) {
            Try(delegate { Hw.GetFanCount(); Hw.SetMaxFan(on); }, what);
            if (!on) { maxAskedAt = DateTime.MinValue; return; }
            if (Route == FanRoute.Ec) WriteLevelsEc(P.Curve.Ceiling, P.Curve.Ceiling, what + " via EC");
            else if (S.MaxIgnored) WriteLevels(P.Curve.Ceiling, P.Curve.Ceiling, what + " as a level");

            else if (!maxProven && maxAskedAt == DateTime.MinValue && !guardStalled && lastFanSeen != 0) { maxAskedAt = DateTime.Now; maxAskedFrom = lastFanSeen; }
        }
        DateTime maxAskedAt = DateTime.MinValue;
        int maxAskedFrom, lastFanSeen = -1;
        bool maxProven;
        int maxIgnoredVerdicts;

        void ApplyFanCore() {
            if (GuardActive) { GuardFans(); return; }
            switch (S.Fan) {
                case FanMode.Max: MaxFan(true, "Max fan"); break;
                case FanMode.Manual: MaxFan(false, "Max fan off"); WriteLevels(S.Fan1, S.Fan2, "Fan level"); break;
                default: MaxFan(false, "Max fan off"); AutoTick(true); break;
            }
        }

        double smoothCpu = double.NaN, smoothGpu = double.NaN;
        int cpuGone, gpuGone;
        const double Smoothing = 0.4;
        const int Deadband = 2;


        static double Smooth(double now, double was, ref int gone) {
            if (double.IsNaN(now)) { gone++; return gone >= 3 ? double.NaN : was; }
            gone = 0;
            if (double.IsNaN(was)) return now;
            if (now > was) return now;
            return was + (now - was) * Smoothing;
        }

        void AutoTick(bool immediate) {
            if ((S.Fan != FanMode.Auto && S.Fan != FanMode.Custom) || GuardActive || ReadOnly) return;
            if (fanWriteFailures >= 3 && (DateTime.Now - lastFanWrite).TotalSeconds < 60) return;
            try { IrTemp = Hw.GetTemperature(); } catch { IrTemp = double.NaN; }
            if (immediate) { smoothCpu = CpuTemp; smoothGpu = GpuTemp; cpuGone = gpuGone = 0; }
            else { smoothCpu = Smooth(CpuTemp, smoothCpu, ref cpuGone); smoothGpu = Smooth(GpuTemp, smoothGpu, ref gpuGone); }
            FanCurve curve = S.Fan == FanMode.Custom ? CustomCurve() : P.Curve;
            int[] target = curve.Target(smoothCpu, smoothGpu, IrTemp);
            int n1 = immediate ? target[0] : curve.Step(curLevel1, target[0]);
            int n2 = immediate ? target[1] : curve.Step(curLevel2, target[1]);
            bool changed = n1 != curLevel1 || n2 != curLevel2;
            bool refresh = (DateTime.Now - lastFanWrite).TotalSeconds >= 30;

            if (changed && !immediate && !refresh && curLevel1 > 0 && n1 <= curLevel1 && n2 <= curLevel2
                && Math.Abs(n1 - curLevel1) < Deadband && Math.Abs(n2 - curLevel2) < Deadband) changed = false;
            if (!changed && !refresh) return;
            bool ok = WriteLevels(n1, n2, "Fan curve");
            lastFanWrite = DateTime.Now;
            if (changed && ok) Log.Write("curve " + n1 + "/" + n2 + " (target " + target[0] + ", cpu " + Fmt(smoothCpu) + " gpu " + Fmt(smoothGpu) + " ir " + Fmt(IrTemp) + ")");
        }
        DateTime lastFanWrite = DateTime.MinValue;
        public static readonly int[] CurveTemps = { 30, 40, 50, 60, 70, 80, 90 };

        FanCurve CustomCurve() {
            var lv = S.Cur.CurveLevels;
            var gl = S.Cur.CurveLinked ? lv : S.Cur.GpuCurveLevels;

            int floor = Math.Max(0, Math.Min(P.Curve.Ceiling, S.Cur.CurveFloor));
            int step = Math.Max(1, Math.Min(P.Curve.Ceiling, (int)Math.Round(P.Curve.StepPerTick * 5.0 / Math.Max(1, S.Cur.CurveRamp))));

            return new FanCurve { CpuTemps = CurveTemps, CpuLevels = lv, GpuTemps = CurveTemps, GpuLevels = gl, IrTemps = P.Curve.IrTemps, IrLevels = P.Curve.IrLevels,
                Floor = floor, Ceiling = P.Curve.Ceiling, StepPerTick = step, Fallback = Math.Max(floor, P.Curve.Fallback),
                UseChassis = P.Curve.UseChassis, Linked = S.Cur.CurveLinked };
        }

        public int[] VendorCurveAt(bool gpu) {
            var r = new int[CurveTemps.Length];
            for (int i = 0; i < r.Length; i++) r[i] = gpu ? P.Curve.Target(double.NaN, CurveTemps[i], double.NaN)[0] : P.Curve.Target(CurveTemps[i], double.NaN, double.NaN)[0];
            return r;
        }

        public void SetCurve(int[] levels, bool gpu) {
            if (levels == null || levels.Length != CurveTemps.Length) return;
            var lv = new int[levels.Length];
            for (int i = 0; i < lv.Length; i++) lv[i] = P.Curve.ClampOrOff(levels[i]);
            if (gpu) S.Cur.GpuCurveLevels = lv;
            else S.Cur.CurveLevels = lv;
            S.Save();
            if (S.Fan == FanMode.Custom && !GuardActive) lock (applySync) { AutoTick(true); lastFanWrite = DateTime.Now; }
            Changed();
        }
        public void SetCurveFloor(int level) {
            S.Cur.CurveFloor = level <= P.Curve.Floor ? 0 : P.Curve.Clamp(level);
            S.Save();
            if (S.Fan == FanMode.Custom && !GuardActive) lock (applySync) { AutoTick(true); lastFanWrite = DateTime.Now; }
            Changed();
        }
        public void SetCurveRamp(int seconds) { S.Cur.CurveRamp = Math.Max(1, Math.Min(10, seconds)); S.Save(); Changed(); }
        public void SetMaxBackWhenCool(bool on) { S.MaxBackWhenCool = on; if (!on) maxCoolSince = DateTime.MinValue; S.Save(); Changed(); }
        public void SetMaxStopAfter(int minutes) { S.MaxStopAfterMin = Math.Max(0, minutes); S.Save(); Changed(); }
        public void SetManualLinked(bool on) { S.ManualLinked = on; S.Save(); Changed(); }

        public void SeedCurveFromVendor() {
            S.Cur.CurveLevels = VendorCurveAt(false);
            S.Cur.GpuCurveLevels = VendorCurveAt(true);
            S.Save();
            SetFan(FanMode.Custom, S.Fan1, S.Fan2, false);
        }
        public void SetCurveLinked(bool linked) {
            if (S.Cur.CurveLinked == linked) return;
            if (!linked) S.Cur.GpuCurveLevels = (int[])S.Cur.CurveLevels.Clone();
            S.Cur.CurveLinked = linked;
            S.Save();
            if (S.Fan == FanMode.Custom && !GuardActive) lock (applySync) { AutoTick(true); lastFanWrite = DateTime.Now; }
            Changed();
        }
        static string Fmt(double v) { return double.IsNaN(v) ? "?" : v.ToString("0"); }

        public string Rpm(int level) {
            if (level < 0) return "--";
            return P.RpmPerLevel > 0 ? (level * P.RpmPerLevel).ToString(CultureInfo.InvariantCulture) + " rpm" : Percent(level) + " of top speed";
        }
        public string Percent(int level) { return (int)Math.Round(100.0 * level / Math.Max(1, P.Curve.Ceiling)) + "%"; }
        void ApplyPowerCore() { if (P.HasPowerGain) Try(delegate { Hw.SetConcurrentTdp(CurrentTdp); }, "Set power"); }
        void ApplyGpuCore() {
            if (!P.HasGpuPower) return;

            GpuLevel g = EffectiveGpu;
            byte[] p = g == GpuLevel.Max ? P.GpuMax : g == GpuLevel.Boost ? P.GpuBoost : P.GpuBase;
            Try(delegate { Hw.SetGpuPower(p[0] != 0, p[1] != 0, p[3]); }, "GPU power");
        }

        public void SetMode(int index, bool announce) {
            if (ecoForcedByBattery) { ecoForcedByBattery = false; S.SavedModeOverride = -1; }
            SetModeCore(index, announce);
        }
        void SetModeCore(int index, bool announce) {
            index = Math.Max(0, Math.Min(2, index));
            S.ModeIndex = index;
            S.Save();
            NoteFanMode(S.Fan);
            lock (applySync) {

                if (Try(delegate { Hw.SetMode(ModeByte, FansByBios); }, "Set mode")) { if (announce) Say(ModeName + " mode"); }
                if (!GuardActive) { ApplyFanCore(); lastFanWrite = DateTime.Now; }
                ApplyPowerCore();
                ApplyGpuCore();
                if (S.SyncWinPower) SetWinPowerOverlay(index);
            }
            Changed();
        }

        public void SetEcoCool(bool on) {
            S.EcoCool = on;
            S.Save();
            if (ModeIndex == 0) lock (applySync) Try(delegate { Hw.SetMode(ModeByte, FansByBios); }, "Set mode");
            Changed();
        }
        public void SetFan(FanMode mode, int f1, int f2, bool announce) {


            if (mode == FanMode.Max && S.Fan != FanMode.Max) { maxSince = DateTime.MinValue; fanBeforeMax = S.Fan; }
            NoteFanMode(mode);
            S.Fan = mode;
            S.Fan1 = P.Curve.ClampOrOff(f1);
            S.Fan2 = P.Curve.ClampOrOff(f2);
            S.Save();
            if (GuardActive && mode != FanMode.Max) { Say("Thermal guard is holding max fan; " + Choice.Fan[Choice.Of(mode)] + " resumes when cool"); Changed(); return; }


            if (OnBattery) lock (applySync) Try(delegate { Hw.SetMode(ModeByte, FansByBios); }, "Set mode");
            lock (applySync) { ApplyFanCore(); lastFanWrite = DateTime.Now; }
            if (announce) Say(mode == FanMode.Max ? "Max fan" : mode == FanMode.Manual ? "Fans " + Rpm(S.Fan1) + " / " + Rpm(S.Fan2) : mode == FanMode.Custom ? "Fans on your curve" : "Fans auto");
            Changed();
        }
        public void ToggleMaxFan() { SetFan(S.Fan == FanMode.Max ? fanBeforeMax : FanMode.Max, S.Fan1, S.Fan2, false); }

        public void SetTdpOffset(int off, bool announce) {
            S.TdpOffset = Math.Max(0, Math.Min(MaxOffset, off));
            S.Save();
            lock (applySync) ApplyPowerCore();
            if (announce) Say("Power gain +" + S.TdpOffset + " W · " + CurrentTdp + " W budget");
            Changed();
        }

        public void SetGpu(GpuLevel lvl, bool auto, bool announce) {
            S.Gpu = lvl;
            S.GpuAuto = auto;
            S.Save();
            lock (applySync) ApplyGpuCore();
            if (announce) Say("GPU " + (auto ? "auto" : lvl.ToString()));
            Changed();
        }

        public void SetKey(KeyAction a) { S.Key = a; S.Save(); Changed(); }
        public void SetKeyCommand(string cmd) { S.KeyCommand = (cmd ?? "").Trim(); S.Save(); Changed(); }
        public void SetHotkeys(bool on) { S.Hotkeys = on; S.Save(); Changed(); }


        public Hotkey GetHotkey(HotkeyAction a) {
            string t = S.HotkeyText[(int)a];
            if (t == null) return HotkeyTable.Defaults[(int)a];
            Hotkey h;
            return Hotkey.TryParse(t, out h) ? h : HotkeyTable.Defaults[(int)a];
        }
        public Hotkey[] GetHotkeys() { var r = new Hotkey[HotkeyTable.Count]; for (int i = 0; i < r.Length; i++) r[i] = GetHotkey((HotkeyAction)i); return r; }
        public bool HotkeysCustomised { get { foreach (string t in S.HotkeyText) if (t != null) return true; return false; } }


        public void SetHotkey(HotkeyAction a, Hotkey h) {
            for (int i = 0; i < HotkeyTable.Count; i++)
                if (i != (int)a && !h.IsEmpty && GetHotkey((HotkeyAction)i).Same(h)) S.HotkeyText[i] = HotkeyTable.Defaults[i].Same(Hotkey.None) ? null : "";
            S.HotkeyText[(int)a] = h.Same(HotkeyTable.Defaults[(int)a]) ? null : h.ToString();
            S.Save();
            Changed();
        }
        public void ResetHotkeys() { for (int i = 0; i < HotkeyTable.Count; i++) S.HotkeyText[i] = null; S.Save(); Changed(); }


        public string[] SnapshotHotkeys() { return (string[])S.HotkeyText.Clone(); }
        public void RestoreHotkeys(string[] snapshot) { Array.Copy(snapshot, S.HotkeyText, HotkeyTable.Count); S.Save(); Changed(); }
        public void SetEcoOnBattery(bool on) { S.EcoOnBattery = on; S.Save(); Changed(); }
        public void SetSyncWinPower(bool on) { S.SyncWinPower = on; S.Save(); if (on) SetWinPowerOverlay(ModeIndex); Changed(); }
        public void SetLowHzOnBattery(bool on) { S.LowHzOnBattery = on; S.Save(); Changed(); }
        public void SetTrayTemp(bool on) { S.TrayTemp = on; S.Save(); Changed(); }
        public void SetUpdateOnLaunch(bool on) { S.UpdateOnLaunch = on; S.Save(); Changed(); }


        void Heartbeat() {
            try {
                if (!BiosOk && !Hw.IsDemo) return;
                lock (applySync) {
                    if (GuardActive) GuardFans();
                    Try(delegate { Hw.SetMode(ModeByte, FansByBios); }, "Set mode");
                    ApplyPowerCore();
                    LastHeartbeat = DateTime.Now;
                }
                if (S.SuppressOgh && !Hw.IsDemo && Supported) KillOgh();
            } catch (Exception ex) { Log.Write("heartbeat: " + ex.Message); }
        }

        public volatile bool GuardActive;
        public double CpuTemp = double.NaN;
        public double CpuTempNow = double.NaN;
        public double CpuLoad = double.NaN;
        public double GpuLoad = double.NaN;
        public bool SensorsSeen = false;
        public int GuardChassis = -1;

        public void SetOverlay(bool on) {
            S.Overlay = on;
            S.Save();
            Changed();
        }

        public void SetOverlayPerf(int perfIdx) {
            S.OverlayPerfMode = Math.Max(0, Math.Min(3, perfIdx));
            S.Save();
            if (perfIdx == 0) {
                SetMode(0, false);
            } else if (perfIdx == 1) {
                SetMode(0, false);
                SetFan(FanMode.Auto, S.Fan1, S.Fan2, false);
            } else if (perfIdx == 2) {
                SetMode(1, false);
            } else if (perfIdx == 3) {
                SetMode(2, false);
            }
            Changed();
        }
        DateTime guardSafeSince = DateTime.MinValue;
        int guardHotTicks;
        int guardWarmTicks;
        int guardIgnoredTicks;
        bool chassisScaleKnown;
        System.Threading.Timer guard;

        public int GuardCpuHot { get { return S.GuardCpu > 0 ? S.GuardCpu : P.Guard.CpuHot; } }
        public int GuardChassisHot { get { return S.GuardChassis > 0 ? S.GuardChassis : P.Guard.ChassisHot; } }

        public int GuardCpuSafe { get { return GuardCpuHot - (P.Guard.CpuHot - P.Guard.CpuSafe); } }
        public int GuardChassisSafe { get { return GuardChassisHot - (P.Guard.ChassisHot - P.Guard.ChassisSafe); } }
        public int GuardHoldSeconds { get { return S.GuardHold > 0 ? S.GuardHold : P.Guard.SafeSeconds; } }
        public int GuardLevel { get { return S.GuardLevel; } }
        public void SetGuardLimits(int cpu, int chassis, int level, int hold) {
            S.GuardCpu = cpu == P.Guard.CpuHot ? 0 : cpu;
            S.GuardChassis = chassis == P.Guard.ChassisHot ? 0 : chassis;
            S.GuardLevel = Math.Max(0, level);
            S.GuardHold = hold == P.Guard.SafeSeconds ? 0 : hold;
            S.Save();
            if (GuardActive) lock (applySync) GuardFans();
            Changed();
        }
        bool guardStalled;


        void GuardFans() {

            if (GuardLevel <= 0 || guardStalled || !CanSetFanLevels || FansByBios || S.Fan == FanMode.Max) { MaxFan(true, "Guard max fan"); return; }

            int level = Math.Max(GuardLevel, Math.Max(curLevel1, curLevel2));
            MaxFan(false, "Guard level");
            WriteLevels(level, level, "Guard level");
        }
        public void SetGuard(bool on) {
            S.Guard = on;
            S.Save();
            if (!on && GuardActive) { GuardActive = false; guardSafeSince = DateTime.MinValue; guardStalled = false; guardIgnoredTicks = guardHotTicks = guardWarmTicks = 0; curLevel1 = curLevel2 = -1; lock (applySync) { ApplyFanCore(); lastFanWrite = DateTime.Now; } Log.Write("thermal guard switched off while engaged; fans back to " + S.Fan); }
            Changed();
        }
        void GuardTick() {
            if (!BiosOk || Hw.IsDemo || ReadOnly) return;
            if (!S.Guard) {
                if (GuardActive) { GuardActive = false; guardSafeSince = DateTime.MinValue; guardStalled = false; guardIgnoredTicks = guardHotTicks = guardWarmTicks = 0; curLevel1 = curLevel2 = -1; lock (applySync) { ApplyFanCore(); lastFanWrite = DateTime.Now; } Changed(); }


                try { int[] idle; lock (applySync) idle = Hw.GetFanLevels(); NoteFanLevels(idle); } catch { }
                return;
            }
            try {
                int[] f;
                int c;
                lock (applySync) { f = Hw.GetFanLevels(); c = Hw.GetTemperature(); }
                NoteFanLevels(f);
                GuardChassis = c;
                double t = CpuTemp;
                bool cpuKnown = !double.IsNaN(t);


                if (c >= 0 && c < GuardChassisSafe) chassisScaleKnown = true;
                bool chassisUsable = P.Verified || chassisScaleKnown;
                bool hot = (cpuKnown && t >= GuardCpuHot) || (chassisUsable && c >= GuardChassisHot);
                bool stalled = cpuKnown && t >= P.Guard.StallCpu && f[0] >= 0 && f[1] >= 0 && (f[0] + f[1]) < P.Guard.StallLevelSum;

                if (hot || stalled) guardHotTicks++; else guardHotTicks = 0;
                if ((hot || stalled) && !GuardActive && guardHotTicks >= 2) {
                    GuardActive = true;
                    guardStalled = stalled;
                    guardSafeSince = DateTime.MinValue;
                    Log.Write("THERMAL GUARD engaged: cpu=" + (cpuKnown ? t.ToString("0") : "?") + " ambient=" + c + " fans=" + f[0] + "/" + f[1] + (stalled ? " (stalled)" : ""));


                    Fire(Toast, "Thermal guard: fans to max (CPU " + (cpuKnown ? t.ToString("0") + "°" : "?") + ", ambient " + c + "°)", true);
                    lock (applySync) GuardFans();
                    Changed();
                } else if (GuardActive) {

                    if (f[0] >= 0 && f[1] >= 0 && (f[0] + f[1]) < P.Guard.StallLevelSum) guardIgnoredTicks++; else guardIgnoredTicks = 0;
                    if (guardIgnoredTicks == 3) {
                        Log.Write("THERMAL GUARD is being ignored: commanded every tick and the firmware still reports "
                            + f[0] + "/" + f[1] + ". Fan commands are not reaching the fans.");
                        Fire(Toast, "The fans are not answering the thermal guard. Save your work and restart the machine.", true);

                        guardStalled = true;
                        lock (applySync) GuardFans();
                    }
                    bool safe = (!cpuKnown || t < GuardCpuSafe) && (!chassisUsable || c < GuardChassisSafe);

                    if (safe) guardWarmTicks = 0; else guardWarmTicks++;
                    if (guardWarmTicks >= 2) { guardSafeSince = DateTime.MinValue; lock (applySync) GuardFans(); }
                    else if (!safe) { lock (applySync) GuardFans(); }
                    else if (guardSafeSince == DateTime.MinValue) guardSafeSince = DateTime.Now;
                    else if ((DateTime.Now - guardSafeSince).TotalSeconds >= GuardHoldSeconds) {
                        GuardActive = false;
                        guardStalled = false;
                        guardIgnoredTicks = 0;
                        Log.Write("thermal guard released; fan mode back to " + S.Fan);
                        Fire(Toast, "Thermal guard released", false);
                        curLevel1 = curLevel2 = -1;
                        lock (applySync) { ApplyFanCore(); lastFanWrite = DateTime.Now; }
                        Changed();
                    }
                }
            } catch (Exception ex) { Log.Write("guard: " + ex.Message); }
        }

        public void OnResume() {

            new Thread(delegate() { Thread.Sleep(4000); Log.Write("resume: re-applying"); ApplyAll(false); }) { IsBackground = true }.Start();
        }

        bool FansByBios { get { return OnBattery && S.Fan == FanMode.Auto; } }
        public void OnPowerSource(bool onBattery) {
            bool changed = OnBattery != onBattery;
            OnBattery = onBattery;
            ApplyRefreshRate(onBattery);
            if (S.EcoOnBattery) {

                if (onBattery && !ecoForcedByBattery && ModeIndex != 0) { ecoForcedByBattery = true; modeBeforeBattery = ModeIndex; S.SavedModeOverride = modeBeforeBattery; Say("On battery → Eco"); SetModeCore(0, false); return; }
                if (!onBattery && ecoForcedByBattery) { ecoForcedByBattery = false; S.SavedModeOverride = -1; SetModeCore(modeBeforeBattery, false); Say("Plugged in → " + ModeName); return; }
            }
            if (changed) lock (applySync) Try(delegate { Hw.SetMode(ModeByte, FansByBios); }, "Set mode");
        }

        public void KillOgh() {
            string[] names = new string[] {
                "OmenCommandCenterBackground", "OmenInstallMonitor",
                "ArmouryCrate.UserSessionHelper", "ArmouryCrate.Service", "ArmourySocketServer",
                "AsusAppService", "AsusSystemAnalysis", "AsusLinkNear", "ROG Live Service",
                "PredatorSense", "NitroSense", "AcerGamingCenter", "AcerNitroSenseService", "PredatorSenseService", "AcerCareCenter"
            };
            foreach (string name in names) {
                try {
                    foreach (var p in Process.GetProcessesByName(name)) {
                        using (p) { try { p.Kill(); Log.Write("stopped " + name + " (pid " + p.Id + ")"); } catch (Exception ex) { Log.Write("kill " + name + ": " + ex.Message); } }
                    }
                } catch { }
            }
        }

        public void SetOghTasks(bool disable) {
            if (Hw.IsDemo) return;
            try {
                var psi = new ProcessStartInfo("schtasks.exe", "/Query /FO CSV /NH") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                string csv = "";
                var outLines = new System.Text.StringBuilder();
                using (var q = new Process()) {
                    q.StartInfo = psi;
                    q.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (outLines) outLines.Append(e.Data).Append("\n"); };
                    q.Start();
                    q.BeginOutputReadLine();
                    if (q.WaitForExit(5000)) { q.WaitForExit(); csv = outLines.ToString(); }
                    else { try { q.Kill(); } catch { } q.WaitForExit(2000); if (!q.HasExited) Log.Write("could not stop schtasks query"); Log.Write("schtasks query timed out"); }
                }
                int n = 0;
                foreach (string line in csv.Split('\n')) {
                    if (!line.StartsWith("\"\\OmenInstallMonitor", StringComparison.OrdinalIgnoreCase)) continue;
                    string task = line.Split('"')[1];
                    using (var c = Process.Start(new ProcessStartInfo("schtasks.exe", "/Change /TN \"" + task + "\" " + (disable ? "/DISABLE" : "/ENABLE")) { CreateNoWindow = true, UseShellExecute = false })) {
                        if (c.WaitForExit(5000)) { if (c.ExitCode == 0) n++; else Log.Write("schtasks " + task + " rc=" + c.ExitCode); }
                        else { try { c.Kill(); } catch { } c.WaitForExit(2000); if (!c.HasExited) Log.Write("could not stop " + task); Log.Write("schtasks " + task + " timed out"); }
                    }
                }
                Log.Write((disable ? "disabled " : "re-enabled ") + n + " OmenInstallMonitor task(s)");
            } catch (Exception ex) { Log.Write("OGH tasks: " + ex.Message); }
        }

        public void SetOghSuppression(bool on) {
            S.SuppressOgh = on;
            S.Save();
            if (Hw.IsDemo) { Say("Simulated hardware: vendor gaming apps are left alone"); Changed(); return; }
            if (ReadOnly) { Say("Unsupported board: vendor gaming apps keep the key"); Changed(); return; }
            if (on) { KillOgh(); SetOghTasks(true); Say("The gaming key now belongs to " + Program.DisplayName); }
            else { SetOghTasks(false); Say("Vendor gaming app restored at next logon"); }
        }


        void StartKeyWatcher() {
            var vendor = Platforms.ReadVendor();
            if (vendor == LaptopVendor.Asus) {
                try {
                    var w = new ManagementEventWatcher(new ManagementScope("root\\wmi"), new WqlEventQuery("SELECT * FROM AsusAtkWmi_Event"));
                    w.EventArrived += delegate(object s, EventArrivedEventArgs e) {
                        try {
                            object val = e.NewEvent["EventID"];
                            if (val == null) val = e.NewEvent["Data"];
                            uint code = val != null ? Convert.ToUInt32(val, CultureInfo.InvariantCulture) : 0;
                            Log.Write("AsusAtkWmi_Event code=" + code);
                            if (code == 0x38 || code == 0xB5 || code == 0xAE) {
                                var h = KeyPressed;
                                if (h != null && S.Key != KeyAction.Off) { try { h(S.Key); } catch { } }
                                else SetMode((ModeIndex + 1) % 3, true);
                            }
                        } catch { }
                    };
                    w.Start();
                    watcher = w;
                    Log.Write("AsusAtkWmi_Event watcher started");
                    return;
                } catch (Exception ex) { Log.Write("AsusAtkWmi_Event watcher: " + ex.Message); }
            } else if (vendor == LaptopVendor.Acer) {
                try {
                    var w = new ManagementEventWatcher(new ManagementScope("root\\wmi"), new WqlEventQuery("SELECT * FROM Acer_WMI_Event"));
                    w.EventArrived += delegate(object s, EventArrivedEventArgs e) {
                        try {
                            Log.Write("Acer_WMI_Event received");
                            var h = KeyPressed;
                            if (h != null && S.Key != KeyAction.Off) { try { h(S.Key); } catch { } }
                            else SetMode((ModeIndex + 1) % 3, true);
                        } catch { }
                    };
                    w.Start();
                    watcher = w;
                    Log.Write("Acer_WMI_Event watcher started");
                    return;
                } catch (Exception ex) { Log.Write("Acer_WMI_Event watcher: " + ex.Message); }
            }

            try {
                var w = new ManagementEventWatcher(new ManagementScope("root\\wmi"), new WqlEventQuery("SELECT * FROM hpqBEvnt"));
                w.EventArrived += OnBiosEvent;
                w.Start();
                watcher = w;
                Log.Write("hpqBEvnt watcher started");
            } catch (Exception ex) { Log.Write("hpqBEvnt watcher failed: " + ex.Message); }
        }

        void OnBiosEvent(object s, EventArrivedEventArgs e) {
            uint id = 0, data = 0;
            try { id = Convert.ToUInt32(e.NewEvent["EventID"]); data = Convert.ToUInt32(e.NewEvent["EventData"]); } catch { return; }
            LastEventId = id;
            LastEventData = data;
            LastEventTime = DateTime.Now;
            Log.Write("hpqBEvnt id=" + id + " data=" + data);
            var any = AnyKeyEvent; if (any != null) { try { any(id, data); } catch { } }
            if (Learning) {
                if (id == 131073) return;
                Learning = false;
                S.KeyId = id;
                S.KeyData = data;
                S.Save();
                Say("OMEN key bound to event " + id + "/" + data);
                Changed();
                return;
            }
            if (id == 13 && Light != null && S.Light != 2) {
                S.Light = data == 0 ? 0 : 1;
                S.Save();
                Changed();
                return;
            }
            if (id != KeyId || data != KeyData) return;
            if ((DateTime.Now - lastKey).TotalMilliseconds < 400) return;
            lastKey = DateTime.Now;
            if (S.SuppressOgh && !Hw.IsDemo && Supported) KillOgh();
            if (S.Key == KeyAction.Off) return;
            var h = KeyPressed; if (h != null) { try { h(S.Key); } catch { } }
        }

        public bool SetGpuMode(int mode) {
            if (mode < 0 || mode > 3 || !GpuModeOffered(mode)) return false;
            bool ok; lock (applySync) ok = Try(delegate { Hw.SetGpuMode(mode); }, "Graphics mode");
            if (ok) { GpuModePending = mode; Log.Write("graphics mode " + GpuModeNames[mode] + " written; live after a restart"); }
            Changed();
            return ok;
        }


        void ApplyRefreshRate(bool onBattery) {
            try {
                int[] rates = Display.Rates();
                if (rates.Length < 2) return;
                int want = S.LowHzOnBattery && onBattery ? Display.BatteryHz()
                         : S.RefreshHz > 0 ? S.RefreshHz
                         : S.LowHzOnBattery ? Display.HighestHz()
                         : 0;
                if (want > 0 && Display.CurrentHz() != want) Display.SetHz(want);
            } catch (Exception ex) { Log.Write("refresh rate: " + ex.Message); }
        }
        public void SetRefreshRate(int hz) {
            S.RefreshHz = hz;
            S.Save();
            if (Display.SetHz(hz)) Say(hz + " Hz");
            else Fire(Toast, "Could not switch to " + hz + " Hz", true);
            Changed();
        }


        void InitLight() {
            try {
                Light = Hw.IsDemo ? (ILighting)new DemoLighting() : (BiosOk ? BiosLighting.Detect() : null);


                if (!Hw.IsDemo && Light != null && Light.Inert) {
                    var la = LampArray.FindKeyboard();
                    if (la != null) Light = new PerKeyLighting(la);
                    else Log.Write("per-key board with no HID lighting interface we can drive; colours left to Windows");
                    WinLighting.Warm();
                }
                if (Light == null) return;

                if (Light.Kind == LightKind.PerKey && !S.PerKeyReset) {
                    S.LightColors = "";
                    S.PerKeyReset = true;
                    S.Save();
                    Log.Write("cleared the saved per-key colours once; they were the old all-white default");
                }
                var fw = Light.GetColors();
                LightColors = ParseColors(S.LightColors, Light.Zones);
                if (LightColors == null) { LightColors = fw; S.LightColors = JoinColors(fw); }
                if (S.Light < 0) S.Light = WinLighting.HasControl ? 2 : ((Light.GetBacklight() & BiosLighting.ON_FLAG) != 0 ? 1 : 0);
                Log.Write("lighting: " + Light.Describe + ", mode " + S.Light + ", effect " + S.LightEffect + ", level " + S.LightLevel + (WinLighting.Present ? ", Windows Dynamic Lighting present" + (WinLighting.HasControl ? " (in control)" : "") : ""));
            } catch (Exception ex) { Log.Write("lighting init: " + ex.Message); Light = null; }
        }
        static Rgb[] ParseColors(string s, int n) {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(',');
            var r = new Rgb[n];
            for (int i = 0; i < n; i++) { Rgb c; if (i < parts.Length && Rgb.TryParse(parts[i].Trim(), out c)) r[i] = c; else return null; }
            return r;
        }
        static string JoinColors(Rgb[] c) { var s = new List<string>(); foreach (var x in c) s.Add(x.Hex); return string.Join(",", s.ToArray()); }
        Rgb[] Scaled(Rgb[] c) { var r = new Rgb[c.Length]; double f = Math.Max(0.05, S.LightLevel / 100.0); for (int i = 0; i < c.Length; i++) r[i] = c[i].Scale(f); return r; }
        Rgb[] Scaled(Rgb[] c, int level) { var r = new Rgb[c.Length]; double f = Math.Max(0.05, level / 100.0); for (int i = 0; i < c.Length; i++) r[i] = c[i].Scale(f); return r; }


        void ReleasePerKey() {
            try { var pk = Light as PerKeyLighting; if (pk != null) pk.Release(); } catch (Exception ex) { Log.Write("release per-key: " + ex.Message); }
        }
        void SyncCosmicByteHardware() {
            Rgb baseColor;
            if (!Rgb.TryParse(S.ExternalColor, out baseColor)) baseColor = new Rgb(63, 140, 255);
            ExternalKeyboards.SendCosmicByteCommand(S.ExternalEffect, S.ExternalSpeed, S.ExternalLevel, baseColor);
        }
        void ApplyLightCore() {
            if (!S.KbdLighting) {
                StopEffect();
                ReleasePerKey();
                return;
            }
            if (Light == null && S.Light != 3) return;
            StopEffect();
            if (S.Light == 3) {
                ReleasePerKey();
                ExternalKeyboards.Detect(false);
                SyncCosmicByteHardware();
                StartEffect();
                return;
            }


            if (Light != null && Light.Inert) return;
            if (S.Light == 2) { ReleasePerKey(); WinLighting.SetControl(true); return; }

            if (WinLighting.HasControl) { S.TookWinLighting = true; S.Save(); WinLighting.SetControl(false); }
            TryLight(delegate {
                if (S.Light == 1) Light.SetColors(Scaled(LightColors));
                Light.SetBacklight(S.Light == 1, 100);
            }, "Keyboard lighting");
            if (S.Light == 1 && S.LightEffect != 0) StartEffect();
        }
        public void SetKbdLighting(bool on) {
            S.KbdLighting = on;
            S.Save();
            if (!on) {
                StopEffect();
                ReleasePerKey();
                if (Light != null) {
                    TryLight(delegate {
                        Light.SetColors(new[] { new Rgb(255, 255, 255), new Rgb(255, 255, 255), new Rgb(255, 255, 255), new Rgb(255, 255, 255) });
                        Light.SetBacklight(true, 100);
                    }, "White backlight");
                }
            } else {
                lock (applySync) ApplyLightCore();
            }
            Changed();
        }
        public void SetLight(int mode, int effect, bool announce) {
            S.Light = Math.Max(0, Math.Min(3, mode));
            if (mode == 3) {
                S.ExternalEffect = Math.Max(0, Math.Min(ExternalKeyboards.AnimationNames.Length - 1, effect));
                SyncCosmicByteHardware();
            }
            else S.LightEffect = Math.Max(0, Math.Min(3, effect));
            S.Save();
            lock (applySync) ApplyLightCore();
            if (announce) {
                if (S.Light == 3) Say("Keyboard: External (" + ExternalKeyboards.AnimationNames[Math.Max(0, Math.Min(ExternalKeyboards.AnimationNames.Length - 1, S.ExternalEffect))] + ")");
                else if (S.Light == 2) Say("Keyboard: Windows Dynamic Lighting");
                else if (S.Light == 0) Say("Keyboard off");
                else Say(new[] { "Keyboard static", "Keyboard breathe", "Keyboard cycle", "Keyboard wave" }[S.LightEffect]);
            }
            Changed();
        }
        public void SetExternalEffect(int effect) {
            S.ExternalEffect = Math.Max(0, Math.Min(ExternalKeyboards.AnimationNames.Length - 1, effect));
            S.Save();
            SyncCosmicByteHardware();
            lock (applySync) { if (S.Light == 3) StartEffect(); }
            Changed();
        }
        public void SetExternalSpeed(int speed) {
            S.ExternalSpeed = Math.Max(1, Math.Min(5, speed));
            S.Save();
            SyncCosmicByteHardware();
            Changed();
        }
        public void SetExternalLevel(int level) {
            S.ExternalLevel = Math.Max(5, Math.Min(100, level));
            S.Save();
            SyncCosmicByteHardware();
            Changed();
        }
        public void SetExternalColor(string hex) {
            S.ExternalColor = hex;
            if (S.ExternalEffect == 2 || S.ExternalEffect == 19) S.ExternalEffect = 0;
            S.Save();
            SyncCosmicByteHardware();
            Changed();
        }

        public void SetLightColor(int[] zones, Rgb c) {
            if (Light == null) return;
            for (int i = 0; i < LightColors.Length; i++) if (zones == null || Array.IndexOf(zones, i) >= 0) LightColors[i] = c;
            S.LightColors = JoinColors(LightColors);
            if (S.Light != 1) S.Light = 1;
            S.Save();
            lock (applySync) ApplyLightCore();
            Changed();
        }
        public static double SpeedFactor(int speed) { return new[] { 0.35, 0.6, 1.0, 1.6, 2.4 }[Math.Max(0, Math.Min(4, speed - 1))]; }
        public void SetLightSpeed(int speed) { S.LightSpeed = Math.Max(1, Math.Min(5, speed)); S.Save(); Changed(); }
        public void SetLightLevel(int level) {
            S.LightLevel = Math.Max(0, Math.Min(100, level));
            S.Save();
            lock (applySync) { if (S.Light == 1 && Light != null && S.LightEffect == 0) TryLight(delegate { Light.SetColors(Scaled(LightColors)); }, "Keyboard brightness"); }
            Changed();
        }


        public static Rgb EffectFrame(int effect, double phase, Rgb[] baseColors, int i) {
            switch (effect) {
                case 1: return baseColors[i].Scale(0.15 + 0.85 * (0.5 + 0.5 * Math.Sin(phase * 1.6)));
                case 2: return Rgb.FromHue(phase * 25);
                case 3: return Rgb.FromHue(phase * 25 + i * (360.0 / Math.Max(1, baseColors.Length)));
                default: return baseColors[i];
            }
        }

        System.Threading.Timer fx;
        double fxPhase;
        int fxFailures;
        void StartEffect() { fxFailures = 0; if (fx == null) fx = new System.Threading.Timer(delegate { EffectTick(); }, null, 120, 120); else fx.Change(120, 120); }
        void StopEffect() { if (fx != null) fx.Change(Timeout.Infinite, Timeout.Infinite); }
        void EffectTick() {
            if (S.Light == 3) {
                if (!Monitor.TryEnter(applySync, 50)) return;
                try {
                    fxPhase += 0.12 * SpeedFactor(S.ExternalSpeed);
                    Rgb baseColor;
                    if (!Rgb.TryParse(S.ExternalColor, out baseColor)) baseColor = new Rgb(63, 140, 255);
                    int zoneCount = (Light != null && LightColors != null && LightColors.Length > 0) ? LightColors.Length : 4;
                    var frame = new Rgb[zoneCount];
                    for (int i = 0; i < frame.Length; i++) {
                        Rgb c = (S.ExternalEffect == 15 && LightColors != null && i < LightColors.Length) ? LightColors[i] : baseColor;
                        frame[i] = ExternalKeyboards.Frame(S.ExternalEffect, fxPhase, c, i, frame.Length);
                    }
                    var scaled = Scaled(frame, S.ExternalLevel);
                    if (Light != null && !Light.Inert) {
                        try { Light.SetColors(scaled); } catch { }
                    }
                    ExternalKeyboards.PushFrameToDevices(scaled, S.ExternalLevel);
                    var fh = FrameChanged;
                    if (fh != null) fh(scaled);
                } finally { Monitor.Exit(applySync); }
                return;
            }
            if (Light == null || S.Light != 1 || S.LightEffect == 0) return;
            if (!Monitor.TryEnter(applySync, 50)) return;
            try {
                fxPhase += 0.12 * SpeedFactor(S.LightSpeed);
                var frame = new Rgb[LightColors.Length];
                for (int i = 0; i < frame.Length; i++) frame[i] = EffectFrame(S.LightEffect, fxPhase, LightColors, i);
                try { var scaled = Scaled(frame); Light.SetColors(scaled); fxFailures = 0; var fh = FrameChanged; if (fh != null) fh(scaled); }
                catch (Exception ex) { if (++fxFailures >= 5) { Log.Write("effect stopped: " + ex.Message); StopEffect(); } }
            } finally { Monitor.Exit(applySync); }
        }


        static readonly Guid OverlayEfficiency = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        static readonly Guid OverlayBalanced = Guid.Empty;
        static readonly Guid OverlayPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");
        [DllImport("powrprof.dll")] static extern uint PowerSetActiveOverlayScheme(ref Guid overlay);
        public void SetWinPowerOverlay(int modeIndex) {
            try {
                Guid g = modeIndex == 0 ? OverlayEfficiency : modeIndex == 2 ? OverlayPerformance : OverlayBalanced;
                uint rc = PowerSetActiveOverlayScheme(ref g);
                if (rc != 0) Log.Write("PowerSetActiveOverlayScheme rc=" + rc);
            } catch (Exception ex) { Log.Write("power overlay: " + ex.Message); }
        }


        public string Diagnostics() {
            var sb = new StringBuilder();
            sb.AppendLine(Program.AppName + " diagnostics " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("hardware: " + (Hw.IsDemo ? "DEMO (simulated)" : (Hw is AsusHardware ? "ASUS WMI" : Hw is AcerHardware ? "Acer WMI/EC" : Hw is Bios ? "HP BIOS via root\\wmi hpqBIntM" : "Generic WMI/ACPI")));
            sb.AppendLine("platform: " + Model + " board " + Board + " -> " + (Supported ? P.Name + (Generic ? " [generic, unverified]" : "") : "UNSUPPORTED (read-only)"));
            sb.AppendLine("bios ok: " + BiosOk + (LastError.Length > 0 ? "  last error: " + LastError : ""));
            sb.AppendLine("fans: " + FanCount + "   system data: " + Info.Hex + "   policy v" + Info.ThermalPolicy + "  swFan=" + Info.SwFanControl + "  PL4=" + Info.DefaultPl4 + "W  baseTdp=" + Info.DefaultConcurrentTdp + "W");
            try { var f = Hw.GetFanLevels(); sb.AppendLine("fan levels: " + f[0] + " / " + f[1] + "  (x100 RPM)"); } catch (Exception ex) { sb.AppendLine("fan levels: " + ex.Message); }
            try { sb.AppendLine("bios temp sensor: " + Hw.GetTemperature() + " C"); } catch (Exception ex) { sb.AppendLine("temp: " + ex.Message); }
            try { sb.AppendLine("max fan: " + Hw.GetMaxFan()); } catch (Exception ex) { sb.AppendLine("max fan: " + ex.Message); }
            try { sb.AppendLine("gpu power: " + Hw.GetGpuPower()); } catch (Exception ex) { sb.AppendLine("gpu power: " + ex.Message); }
            sb.AppendLine("settings: mode=" + ModeName + " (BIOS 0x" + ModeByte.ToString("X2") + (OnBattery ? ", DC" : ", AC") + ") fan=" + S.Fan + " " + S.Fan1 + "/" + S.Fan2 + " tdp=" + CurrentTdp + "W gpu=" + EffectiveGpu + (S.GpuAuto ? "(auto)" : "") + " key=" + KeyId + "/" + KeyData + "→" + S.Key + " ecoCool=" + S.EcoCool);
            sb.AppendLine("graphics: " + (GpuMode >= 0 && GpuMode < 4 ? GpuModeNames[GpuMode] : "unknown") + " (offered mask 0x" + Info.GpuModes.ToString("X2") + ")" + (GpuModePending >= 0 ? " -> " + GpuModeNames[GpuModePending] + " after restart" : ""));
            sb.AppendLine("lighting: " + (Light == null ? "none" : Light.Describe + " mode=" + S.Light + " level=" + S.LightLevel + " colours=" + S.LightColors + " windowsControl=" + WinLighting.HasControl));
            sb.AppendLine("fan drive: written " + curLevel1 + "/" + curLevel2 + "  cpu " + Fmt(CpuTemp) + " (last single reading " + Fmt(CpuTempNow) + ")  gpu " + Fmt(GpuTemp) + "  ir " + Fmt(IrTemp) + "  guard=" + GuardActive + "  writeFailures=" + fanWriteFailures + "  route=" + Route);
            sb.AppendLine("driver: " + (DriverReady ? "PawnIO " + (Hw.IsDemo ? "simulated" : "" + DriverVersion) + " · cpu " + (Cpu != null ? Cpu.Describe : "none")
                + " · ec " + (Ec == null ? "none" : (ecVerified ? Ec.Map.Name : "map rejected") + (Ec.Resting ? " (resting)" : "") + " · " + EcProof) : "none (" + DriverWhy + ")"));
            sb.AppendLine("last heartbeat: " + (LastHeartbeat == DateTime.MinValue ? "never" : LastHeartbeat.ToString("HH:mm:ss")) + "   last key event: " + (LastEventTime == DateTime.MinValue ? "none" : LastEventId + "/" + LastEventData + " at " + LastEventTime.ToString("HH:mm:ss")));
            return sb.ToString();
        }
    }
}

