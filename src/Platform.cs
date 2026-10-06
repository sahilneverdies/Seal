

using System;
using System.Collections.Generic;
using System.Management;

namespace Seal {

    public enum LaptopVendor { Hp, Asus, Acer, Generic }


    public sealed class PlatformProfile {
        public string Name;
        public string[] Boards;
        public LaptopVendor Vendor = LaptopVendor.Hp;
        public int ThermalPolicy = 1;
        public byte ModeEco = 0x30, ModeBalanced = 0x30, ModePerformance = 0x31, ModeCool = 0x50;
        public int TdpBase = 30, TdpGainMax = 15;
        public byte[] GpuBase = { 0, 0, 1, 75 }, GpuBoost = { 0, 1, 1, 87 }, GpuMax = { 1, 1, 1, 87 };
        public uint KeyEventId = 29, KeyEventData = 8613;

        public bool HasPowerGain = true;
        public bool HasGpuPower = true;
        public FanCurve Curve = FanCurve.Transcend14();
        public int RpmPerLevel = 100;
        public GuardLimits Guard = new GuardLimits();
        public bool Verified = true;


        public DriverFor DriverFor = DriverFor.None;

        public EcMap Ec;
        public string Notes;
    }


    [Flags]
    public enum DriverFor { None = 0, FanLevels = 1, MaxFan = 2 }

    public sealed class GuardLimits {

        public int CpuHot = 95, ChassisHot = 62;
        public int CpuSafe = 85, ChassisSafe = 54;
        public int SafeSeconds = 60;
        public int StallCpu = 75, StallLevelSum = 10;
        public int MaxFanCoolBelow = 60, MaxFanCoolSeconds = 120;
        public int WarnAt = 80;
    }

    public sealed class FanCurve {
        public int[] CpuTemps = { 50, 55, 60, 65, 70, 75, 80, 85, 90 };
        public int[] CpuLevels = { 23, 23, 25, 32, 39, 46, 46, 46, 49 };
        public int[] GpuTemps = { 50, 55, 60, 65, 70, 75, 80, 85, 90 };
        public int[] GpuLevels = { 23, 25, 31, 35, 46, 46, 46, 46, 46 };
        public int[] IrTemps = { 40, 52 };
        public int[] IrLevels = { 0, 46 };
        public int Floor = 18, Ceiling = 57;

        public bool UseChassis = true;


        public bool Linked = true;
        public int StepPerTick = 3;
        public int Fallback = 35;

        static int Interp(int[] xs, int[] ys, double x) {
            if (xs.Length == 0) return 0;
            if (x <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
                if (x <= xs[i]) { double t = (x - xs[i - 1]) / (double)(xs[i] - xs[i - 1]); return (int)Math.Round(ys[i - 1] + t * (ys[i] - ys[i - 1])); }
            return ys[ys.Length - 1];
        }


        public int[] Target(double cpu, double gpu, double ir) {


            bool silicon = !double.IsNaN(cpu) || !double.IsNaN(gpu);
            int c = double.IsNaN(cpu) ? 0 : Interp(CpuTemps, CpuLevels, cpu);
            int g = double.IsNaN(gpu) ? 0 : Interp(GpuTemps, GpuLevels, gpu);
            int r = (double.IsNaN(ir) || !UseChassis) ? 0 : Interp(IrTemps, IrLevels, ir);

            if (!Linked && !double.IsNaN(cpu) && !double.IsNaN(gpu))
                return new int[] { Clamp(Math.Max(c, r)), Clamp(Math.Max(g, r)) };
            int lvl = silicon ? Math.Max(c, Math.Max(g, r)) : Math.Max(Fallback, r);
            return new int[] { Clamp(lvl), Clamp(lvl) };
        }


        public int Step(int current, int target) {
            if (target <= 0) return 0;
            if (current < Floor) return Math.Max(Floor, Math.Min(target, Floor + StepPerTick * 2));
            int d = target - current;
            if (Math.Abs(d) <= StepPerTick) return target;
            return current + Math.Sign(d) * StepPerTick;
        }

        public int Clamp(int level) { return Math.Max(Floor, Math.Min(Ceiling, level)); }

        public int ClampOrOff(int level) { return level == 0 ? 0 : Clamp(level); }


        public static FanCurve Transcend14() { return new FanCurve(); }

        public void Rescale(int newCeiling) {
            if (newCeiling == Ceiling || newCeiling <= 0) return;
            double f = newCeiling / (double)Ceiling;
            for (int i = 0; i < CpuLevels.Length; i++) CpuLevels[i] = (int)Math.Round(CpuLevels[i] * f);
            for (int i = 0; i < GpuLevels.Length; i++) GpuLevels[i] = (int)Math.Round(GpuLevels[i] * f);
            for (int i = 0; i < IrLevels.Length; i++) IrLevels[i] = (int)Math.Round(IrLevels[i] * f);
            Fallback = (int)Math.Round(Fallback * f);
            Ceiling = newCeiling;
        }
    }

    public static class Families {

        public static readonly string[] Omen = { "84DA", "84DB", "84DC", "8572", "8573", "8574", "8575", "8600", "8601", "8602", "8603", "8604", "8605", "8606", "8607", "860A",
            "8746", "8747", "8748", "8749", "874A", "8786", "8787", "8788", "878A", "878B", "878C", "87B5", "886B", "886C", "88C8", "88CB", "88D1", "88D2", "88F4", "88F5",
            "88F6", "88F7", "88FD", "88FE", "88FF", "8900", "8901", "8902", "8912", "8917", "8918", "8949", "894A", "89EB", "8A15", "8A42", "8A43", "8BAD", "8C58", "8E41",

            "8A44", "8A4D", "8BA9", "8BAA", "8BAB", "8BB3", "8BC2", "8BCA", "8BCD", "8C76", "8C77", "8C78", "8D26", "8D41", "8D87", "8D88", "8DD6", "8E35" };
        public static readonly string[] OmenForceV0 = { "8607", "8746", "8747", "8748", "8749", "874A" };
        public static readonly string[] Victus = { "88F8", "8A25" };
        public static readonly string[] VictusS = { "8A3D", "8B2F", "8BBE", "8BD4", "8BD5", "8C99", "8C9C" };
        public static bool In(string[] list, string board) { foreach (var b in list) if (string.Equals(b, board, StringComparison.OrdinalIgnoreCase)) return true; return false; }

        public static readonly string[] OmenMax = { "8D41", "8D42", "8D87", "8D88" };

        public static bool EcCandidate(string board) {
            return !string.IsNullOrEmpty(board) && !In(OmenMax, board);
        }
    }

    public static class Platforms {

        static readonly Dictionary<string, int> PolicyWhenFirmwareWontSay = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {

        };

        static int PolicyVersion(string board, SystemInfo info) {
            if (Families.In(Families.OmenForceV0, board)) return 0;
            if (info != null && info.Valid) return info.ThermalPolicy;
            int v;
            if (PolicyWhenFirmwareWontSay.TryGetValue(board ?? "", out v)) return v;
            return -1;
        }

        public static PlatformProfile Generic(string board, SystemInfo info) {
            bool haveInfo = info != null && info.Valid;
            int policy = PolicyVersion(board, info);
            if (policy < 0) return null;

            bool known = Reported(board);
            var p = new PlatformProfile {
                Name = "OMEN/Victus " + board + (known ? " (verified by its owner)" : " (from its own firmware)"),
                Boards = new[] { board }, Verified = known, ThermalPolicy = policy
            };
            p.Curve = FanCurve.Transcend14();
            p.Curve.UseChassis = false;
            if (Families.In(Families.Victus, board)) { p.ModeEco = 0x03; p.ModeBalanced = 0x00; p.ModePerformance = 0x01; p.ModeCool = 0x03; p.Notes = "Victus family (hp-wmi victus_thermal_profile_boards)"; }
            else if (Families.In(Families.VictusS, board)) { p.ModeEco = 0x00; p.ModeBalanced = 0x00; p.ModePerformance = 0x01; p.ModeCool = 0x00; p.Notes = "Victus S family"; }

            else if (policy == 0 && ReadModel().IndexOf("Victus", StringComparison.OrdinalIgnoreCase) >= 0) {
                p.ModeEco = 0x03; p.ModeBalanced = 0x00; p.ModePerformance = 0x01; p.ModeCool = 0x03;
                p.Notes = "thermal policy v0, Victus quiet byte";
            }
            else if (policy == 0) { p.ModeEco = 0x00; p.ModeBalanced = 0x00; p.ModePerformance = 0x01; p.ModeCool = 0x02; p.Notes = "thermal policy v0"; }
            else if (policy == 1) {


                bool listed = Families.In(Families.Omen, board);
                if (!listed) p.ModeCool = p.ModeEco;
                p.Notes = "thermal policy v1" + (listed ? ", listed in hp-wmi" : ", not in hp-wmi: no Cool profile");
            }
            else return null;
            if (!haveInfo) p.Notes += ", from a contributed readback (0x28 unavailable)";


            p.TdpBase = (haveInfo && info.DefaultConcurrentTdp > 0) ? info.DefaultConcurrentTdp : 35;
            p.TdpGainMax = 15;
            p.HasPowerGain = true;
            p.HasGpuPower = false;
            return Equip(p, board);
        }

        static PlatformProfile Equip(PlatformProfile p, string board) {
            if (p == null) return null;
            if (p.Ec == null && Families.EcCandidate(board)) p.Ec = EcMap.Legacy();
            DriverFor need;
            if (p.DriverFor == DriverFor.None && DriverNeeds.TryGetValue(board ?? "", out need)) p.DriverFor = need;
            return p;
        }


        static readonly Dictionary<string, DriverFor> DriverNeeds = new Dictionary<string, DriverFor>(StringComparer.OrdinalIgnoreCase) {
            { "878A", DriverFor.FanLevels },
            { "8786", DriverFor.FanLevels },
        };

        public static readonly PlatformProfile[] Known = {
            new PlatformProfile {
                Name = "HP OMEN Transcend 14 (2024, 14-fb0xxx)",
                Boards = new[] { "8C58" },
                Vendor = LaptopVendor.Hp,
                ThermalPolicy = 1,
                ModeEco = 0x30, ModeBalanced = 0x30, ModePerformance = 0x31, ModeCool = 0x50,
                TdpBase = 30, TdpGainMax = 15,
                GpuBase = new byte[] { 0, 0, 1, 75 }, GpuBoost = new byte[] { 0, 1, 1, 87 }, GpuMax = new byte[] { 1, 1, 1, 87 },
                KeyEventId = 29, KeyEventData = 8613,
                RpmPerLevel = 100,
                Notes = "Core Ultra 9 185H + RTX 4070. Modes, fans, power and GPU verified 2026-09-08 against OMEN Gaming Hub 1101.2608 logs and code; four-zone keyboard lighting verified on the device 2026-09-12."
            },

            new PlatformProfile {
                Name = "ASUS ROG Zephyrus G14 (GA401/GA402/GA403)",
                Boards = new[] { "GA401", "GA402", "GA403" },
                Vendor = LaptopVendor.Asus,
                ThermalPolicy = 1,
                ModeEco = 0x02, ModeBalanced = 0x00, ModePerformance = 0x01, ModeCool = 0x02,
                TdpBase = 35, TdpGainMax = 25,
                RpmPerLevel = 100,
                Notes = "ASUS ROG Zephyrus G14. Modes: Silent=2, Balanced/Performance=0, Turbo=1 via ASUS_WMI."
            },
            new PlatformProfile {
                Name = "ASUS ROG Zephyrus G15 / G16 / M16",
                Boards = new[] { "GA502", "GA503", "GU603", "GU604", "GU605" },
                Vendor = LaptopVendor.Asus,
                ThermalPolicy = 1,
                ModeEco = 0x02, ModeBalanced = 0x00, ModePerformance = 0x01, ModeCool = 0x02,
                TdpBase = 45, TdpGainMax = 25,
                RpmPerLevel = 100,
                Notes = "ASUS ROG Zephyrus 15/16-inch models via ASUS_WMI."
            },

            new PlatformProfile {
                Name = "ASUS ROG Strix G / SCAR Series",
                Boards = new[] { "G512", "G513", "G533", "G614", "G634", "G712", "G713", "G733", "G814", "G834" },
                Vendor = LaptopVendor.Asus,
                ThermalPolicy = 1,
                ModeEco = 0x02, ModeBalanced = 0x00, ModePerformance = 0x01, ModeCool = 0x02,
                TdpBase = 55, TdpGainMax = 30,
                RpmPerLevel = 100,
                Notes = "ASUS ROG Strix / SCAR high-performance gaming laptop profile."
            },

            new PlatformProfile {
                Name = "ASUS ROG Flow Series (X13/Z13/X16)",
                Boards = new[] { "GV301", "GV302", "GZ301", "GV601" },
                Vendor = LaptopVendor.Asus,
                ThermalPolicy = 1,
                ModeEco = 0x02, ModeBalanced = 0x00, ModePerformance = 0x01, ModeCool = 0x02,
                TdpBase = 35, TdpGainMax = 20,
                RpmPerLevel = 100,
                Notes = "ASUS ROG Flow ultraportable gaming series."
            },

            new PlatformProfile {
                Name = "ASUS TUF Gaming A-Series (A15/A16/A17)",
                Boards = new[] { "FA506", "FA507", "FA617", "FA706", "FA707" },
                Vendor = LaptopVendor.Asus,
                ThermalPolicy = 1,
                ModeEco = 0x02, ModeBalanced = 0x00, ModePerformance = 0x01, ModeCool = 0x02,
                TdpBase = 45, TdpGainMax = 25,
                RpmPerLevel = 100,
                Notes = "ASUS TUF Gaming AMD platform via ASUS_WMI."
            },
            new PlatformProfile {
                Name = "ASUS TUF Gaming F-Series (F15/F16/F17/Dash)",
                Boards = new[] { "FX506", "FX507", "FX607", "FX706", "FX707", "FX516", "FX517" },
                Vendor = LaptopVendor.Asus,
                ThermalPolicy = 1,
                ModeEco = 0x02, ModeBalanced = 0x00, ModePerformance = 0x01, ModeCool = 0x02,
                TdpBase = 45, TdpGainMax = 25,
                RpmPerLevel = 100,
                Notes = "ASUS TUF Gaming Intel platform via ASUS_WMI."
            },

            new PlatformProfile {
                Name = "Acer Predator Helios Series (16/18/300/500/700)",
                Boards = new[] { "PH16", "PH18", "PH315", "PH317", "PH517", "PH717" },
                Vendor = LaptopVendor.Acer,
                ThermalPolicy = 1,
                ModeEco = 0x00, ModeBalanced = 0x01, ModePerformance = 0x02, ModeCool = 0x00,
                TdpBase = 55, TdpGainMax = 25,
                RpmPerLevel = 100,
                Notes = "Acer Predator Helios gaming laptop via Acer WMI & EC (Quiet=0, Default=1, Extreme/Turbo=2)."
            },

            new PlatformProfile {
                Name = "Acer Predator Triton Series (14/16/17/300/500)",
                Boards = new[] { "PT14", "PT16", "PT17", "PT314", "PT315", "PT515", "PT516" },
                Vendor = LaptopVendor.Acer,
                ThermalPolicy = 1,
                ModeEco = 0x00, ModeBalanced = 0x01, ModePerformance = 0x02, ModeCool = 0x00,
                TdpBase = 45, TdpGainMax = 20,
                RpmPerLevel = 100,
                Notes = "Acer Predator Triton slim gaming laptop via Acer WMI & EC."
            },

            new PlatformProfile {
                Name = "Acer Nitro Series (Nitro 5/16/17/V)",
                Boards = new[] { "AN515", "AN517", "AN16", "AN17", "ANV15", "ANV16" },
                Vendor = LaptopVendor.Acer,
                ThermalPolicy = 1,
                ModeEco = 0x00, ModeBalanced = 0x01, ModePerformance = 0x02, ModeCool = 0x00,
                TdpBase = 45, TdpGainMax = 20,
                RpmPerLevel = 100,
                Notes = "Acer Nitro gaming laptop via Acer WMI & EC."
            }
        };

        static readonly string[] OwnerReported = { "8748", "8EEC", "8DCF", "88D2", "88EE", "8BAB", "8A26",
                                                   "8BCD", "8BAD", "8787", "8E10", "8BBE", "8A4C", "8BB3", "8BCA", "8BD5", "8BC2", "8E35", "8C76", "8A25", "8D87" };
        public static bool Reported(string board) { return Families.In(OwnerReported, board); }

        public static string BoardOverride;

        public static string ReadBoard() {
            if (!string.IsNullOrEmpty(BoardOverride)) return BoardOverride;
            try {
                using (var s = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard"))
                    foreach (ManagementObject mo in s.Get()) return (mo["Product"] as string ?? "").Trim();
            } catch { }
            return "";
        }

        public static string ReadModel() {
            try {
                using (var s = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem"))
                    foreach (ManagementObject mo in s.Get()) return (mo["Model"] as string ?? "").Trim();
            } catch { }
            return "";
        }

        public static LaptopVendor ReadVendor() {
            try {
                using (var s = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_ComputerSystem")) {
                    foreach (ManagementObject mo in s.Get()) {
                        string m = (mo["Manufacturer"] as string ?? "").Trim();
                        if (m.IndexOf("ASUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.IndexOf("ASUSTeK", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Asus;
                        if (m.IndexOf("Acer", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Acer;
                        if (m.IndexOf("HP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.IndexOf("Hewlett-Packard", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Hp;
                    }
                }
            } catch { }
            try {
                using (var s = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BaseBoard")) {
                    foreach (ManagementObject mo in s.Get()) {
                        string m = (mo["Manufacturer"] as string ?? "").Trim();
                        if (m.IndexOf("ASUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.IndexOf("ASUSTeK", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Asus;
                        if (m.IndexOf("Acer", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Acer;
                        if (m.IndexOf("HP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.IndexOf("Hewlett-Packard", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Hp;
                    }
                }
            } catch { }
            string model = ReadModel();
            if (model.IndexOf("ROG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("TUF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Zephyrus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Strix", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Asus;
            if (model.IndexOf("Predator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Nitro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Helios", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Triton", StringComparison.OrdinalIgnoreCase) >= 0) return LaptopVendor.Acer;
            return LaptopVendor.Hp;
        }

        public static PlatformProfile GenericAsus(string board, string model) {
            string name = !string.IsNullOrEmpty(model) ? ("ASUS " + model) : (!string.IsNullOrEmpty(board) ? ("ASUS Gaming " + board) : "ASUS Gaming Laptop");
            var p = new PlatformProfile {
                Name = name,
                Boards = new[] { board ?? "" },
                Vendor = LaptopVendor.Asus,
                Verified = true,
                ThermalPolicy = 1,
                ModeEco = 0x02,
                ModeBalanced = 0x00,
                ModePerformance = 0x01,
                ModeCool = 0x02,
                TdpBase = 45,
                TdpGainMax = 25,
                HasPowerGain = true,
                HasGpuPower = true,
                RpmPerLevel = 100,
                Notes = "ASUS ROG / TUF Gaming profile via ASUS_WMI (Silent=2, Performance=0, Turbo=1)"
            };
            p.Curve = FanCurve.Transcend14();
            p.Curve.UseChassis = false;
            p.Curve.Ceiling = 65;
            return p;
        }

        public static PlatformProfile GenericAcer(string board, string model) {
            string name = !string.IsNullOrEmpty(model) ? ("Acer " + model) : (!string.IsNullOrEmpty(board) ? ("Acer Gaming " + board) : "Acer Gaming Laptop");
            var p = new PlatformProfile {
                Name = name,
                Boards = new[] { board ?? "" },
                Vendor = LaptopVendor.Acer,
                Verified = true,
                ThermalPolicy = 1,
                ModeEco = 0x00,
                ModeBalanced = 0x01,
                ModePerformance = 0x02,
                ModeCool = 0x00,
                TdpBase = 45,
                TdpGainMax = 20,
                HasPowerGain = true,
                HasGpuPower = true,
                RpmPerLevel = 100,
                Notes = "Acer Predator / Nitro Gaming profile via Acer WMI & EC (Quiet=0, Default=1, Extreme=2)"
            };
            p.Curve = FanCurve.Transcend14();
            p.Curve.UseChassis = false;
            p.Curve.Ceiling = 60;
            return p;
        }

        public static PlatformProfile Find(string board) {
            if (!string.IsNullOrEmpty(board)) {
                foreach (var p in Known) {
                    foreach (var b in p.Boards) {
                        if (string.Equals(b, board, StringComparison.OrdinalIgnoreCase) ||
                            board.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) {
                            Floor(p);
                            return Equip(p, board);
                        }
                    }
                }
            }
            string model = ReadModel();
            if (!string.IsNullOrEmpty(model)) {
                foreach (var p in Known) {
                    foreach (var b in p.Boards) {
                        if (model.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) {
                            Floor(p);
                            return Equip(p, !string.IsNullOrEmpty(board) ? board : b);
                        }
                    }
                }
            }
            return null;
        }

        public static PlatformProfile Floor(PlatformProfile p) {
            if (p != null && p.Curve != null && p.Curve.Floor > 0 && p.Curve.Floor < Bios.AbsoluteFloor) p.Curve.Floor = Bios.AbsoluteFloor;
            return p;
        }
    }
}
