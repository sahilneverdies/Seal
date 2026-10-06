

using System;
using System.Collections.Generic;
using System.Text;

namespace Seal {

    public sealed class ExternalKeyboardInfo {
        public string Name;
        public string Brand;
        public ushort VendorId;
        public ushort ProductId;
        public string Path;
        public bool IsLampArray;
        public LampArray Device;

        public override string ToString() {
            return Brand + (string.IsNullOrEmpty(Name) ? "" : ": " + Name) + (IsLampArray ? " [LampArray]" : " [USB HID]");
        }
    }

    public static class ExternalKeyboards {
        public static readonly string[] AnimationNames = {
            "FIXED",
            "BREATHING",
            "RAINBOW",
            "FLASH",
            "RAINDROPS",
            "CYCLONE",
            "RIPPLES",
            "TWINKLING STARS",
            "SHADOW DISAPPEAR",
            "SNAKE",
            "NEON STREAM",
            "REACTION",
            "SINE-WAVE",
            "SCANNING",
            "WINDMILL",
            "WATERFALL",
            "BLOSSOM",
            "ROTATING STORM",
            "CUSTOM MODE",
            "OFF"
        };

        public static readonly byte[] CosmicByteHwCodes = {
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
            17,
            18,
            1,
            20
        };

        public static readonly string[] AnimationTips = {
            "Static illumination with chosen color",
            "Smooth breathing fade in and out with chosen color",
            "Vibrant rainbow wave rolling across keys",
            "Dynamic rhythmic bursts of light with chosen color",
            "Gentle raindrops falling across key matrix",
            "Rotational cyclone vortex spinning around keyboard",
            "Concentric ripples radiating outward",
            "Twinkling stars glistening at random positions",
            "Keys illuminate upon strike and gracefully fade into shadow",
            "Slithering snake traversing the key rows",
            "Electrifying high-speed neon stream",
            "Reactive burst radiating from struck keys",
            "Harmonic sine-wave undulating across keys",
            "Linear scanning beam traversing back and forth",
            "Spinning windmill arms rotating smoothly",
            "Cascading waterfall flowing down the layout",
            "Expanding petal blossom bursting outwards",
            "Dynamic swirling storm rotating across keys",
            "Illuminates keyboard statically with your chosen color",
            "Turns keyboard lighting off"
        };

        static List<ExternalKeyboardInfo> cachedDevices;
        static readonly object sync = new object();

        public static string GetBrandTips(string brand) { return BrandTip(brand); }
        public static string BrandTip(string brand) {
            if (string.IsNullOrEmpty(brand))
                return "Cosmic Byte Artemis: Hardware lighting active! All 17 lighting modes sync directly with keyboard hardware (or use Fn+\\ to cycle, Fn+↑/↓ brightness, Fn+←/→ speed).";
            string b = brand.ToLowerInvariant();
            if (b.Contains("cosmic") || b.Contains("artemis"))
                return "Cosmic Byte Artemis: Hardware lighting active! All 17 lighting modes sync directly with keyboard hardware (or use Fn+\\ to cycle, Fn+↑/↓ brightness, Fn+←/→ speed).";
            if (b.Contains("razer"))
                return "Razer: Direct Chroma sync active. Supports Razer Synapse Chroma Connect and Windows Dynamic Lighting.";
            if (b.Contains("corsair"))
                return "Corsair: iCUE & HID RGB stream active.";
            if (b.Contains("redragon"))
                return "Redragon: Hardware presets: Fn+Ins/Home/PgUp/Del/End/PgDn, Fn+↑/↓ brightness, Fn+←/→ speed.";
            if (b.Contains("steelseries"))
                return "SteelSeries: Apex Prism lighting active.";
            if (b.Contains("logitech"))
                return "Logitech: G HUB & LIGHTSYNC integration active.";
            if (b.Contains("keychron"))
                return "Keychron: RGB backlight cycle via lightbulb key or Fn+Q/W/E.";
            return "External RGB active. Animations stream directly to connected keyboards.";
        }

        public static string GuessBrand(ushort vid, string name) {
            string n = (name ?? "").ToLowerInvariant();
            if (vid == 0x1532 || n.Contains("razer")) return "Razer";
            if (vid == 0xB6A4 || vid == 0x258A || vid == 0x3554 || vid == 0x0C45 || vid == 0x320F || vid == 0x04D9 || vid == 0x24AE || n.Contains("cosmic") || n.Contains("artemis") || n.Contains("cb-")) return "Cosmic Byte";
            if (vid == 0x1B1C || n.Contains("corsair")) return "Corsair";
            if (vid == 0x1038 || n.Contains("steelseries")) return "SteelSeries";
            if (vid == 0x046D || n.Contains("logitech")) return "Logitech";
            if (n.Contains("redragon")) return "Redragon";
            if (vid == 0x3434 || vid == 0x2F24 || n.Contains("keychron")) return "Keychron";
            if (vid == 0x0951 || vid == 0x03F0 || n.Contains("hyperx")) return "HyperX";
            if (n.Contains("royal kludge") || n.Contains(" rk")) return "Royal Kludge";
            if (n.Contains("glorious")) return "Glorious";
            return "External Keyboard";
        }

        public static List<ExternalKeyboardInfo> Detect(bool forceRefresh) {
            lock (sync) {
                if (cachedDevices != null && !forceRefresh) return cachedDevices;
                var list = new List<ExternalKeyboardInfo>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


                try {
                    foreach (var la in LampArray.All()) {
                        try {
                            if (la.Kind == LampArray.KindKeyboard && la.LampCount >= 8 && la.AnyProgrammable && !la.Internal) {
                                string brand = GuessBrand(la.VendorId, la.Product);
                                string name = !string.IsNullOrEmpty(la.Product) ? la.Product : (brand + " LampArray");
                                string key = la.VendorId.ToString("X4") + ":" + la.ProductId.ToString("X4");
                                if (seen.Add(key)) {
                                    list.Add(new ExternalKeyboardInfo {
                                        Name = name, Brand = brand, VendorId = la.VendorId, ProductId = la.ProductId,
                                        Path = la.Path, IsLampArray = true, Device = la
                                    });
                                    continue;
                                }
                            }
                        } catch { }
                        la.Dispose();
                    }
                } catch { }


                try {
                    var allHid = Hid.Enumerate();


                    foreach (var info in allHid) {
                        bool isArtemis = (info.VendorId == 0xB6A4 && info.ProductId == 0x4091) ||
                                         (info.VendorId == 0x258A && info.ProductId == 0x010C) ||
                                         (info.VendorId == 0x3554 && info.ProductId == 0xFA09);
                        if (isArtemis) {
                            string key = info.VendorId.ToString("X4") + ":" + info.ProductId.ToString("X4");
                            if (seen.Add(key)) {
                                string name = (info.VendorId == 0xB6A4 || info.VendorId == 0x3554)
                                    ? "Cosmic Byte Artemis Wireless Keyboard"
                                    : "Cosmic Byte Artemis Keyboard (Wired)";
                                list.Insert(0, new ExternalKeyboardInfo {
                                    Name = name, Brand = "Cosmic Byte", VendorId = info.VendorId, ProductId = info.ProductId,
                                    Path = info.Path, IsLampArray = false, Device = null
                                });
                            }
                        }
                    }


                    foreach (var info in allHid) {
                        if (info.UsagePage == 1 && info.Usage == 6) {
                            string p = info.Path ?? "";
                            if (p.IndexOf("HID_DEVICE_SYSTEM_VHF", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            if (info.VendorId == 0x0D62 || info.VendorId == 0x0461 || info.VendorId == 0x04F2 || info.VendorId == 0x048D) continue;
                            string key = info.VendorId.ToString("X4") + ":" + info.ProductId.ToString("X4");
                            if (seen.Add(key)) {
                                string brand = GuessBrand(info.VendorId, info.Product);
                                string name = !string.IsNullOrEmpty(info.Product) ? info.Product : (brand + " USB Keyboard");
                                list.Add(new ExternalKeyboardInfo {
                                    Name = name, Brand = brand, VendorId = info.VendorId, ProductId = info.ProductId,
                                    Path = info.Path, IsLampArray = false, Device = null
                                });
                            }
                        }
                    }
                } catch { }

                cachedDevices = list;
                return cachedDevices;
            }
        }

        public static bool SendCosmicByteCommand(int effectIndex, int speed, int level, Rgb color) {
            if (effectIndex < 0 || effectIndex >= CosmicByteHwCodes.Length) return false;
            byte hwCode = CosmicByteHwCodes[effectIndex];
            byte hwSpeed = (byte)Math.Max(0, Math.Min(4, speed - 1));
            byte hwBri = (byte)Math.Max(0, Math.Min(4, (int)Math.Round((level / 100.0) * 4)));

            bool sent = false;
            try {
                var devices = Hid.Enumerate();
                foreach (var dev in devices) {
                    bool isCosmicByte = (dev.VendorId == 0xB6A4 && dev.ProductId == 0x4091) ||
                                        (dev.VendorId == 0x258A && dev.ProductId == 0x010C) ||
                                        (dev.VendorId == 0x3554 && dev.ProductId == 0xFA09) ||
                                        (dev.VendorId == 0x258A);
                    if (!isCosmicByte) continue;
                    if (dev.FeatureLen != 520 && dev.FeatureLen != 41) continue;

                    int len = dev.FeatureLen;
                    byte[] buf = new byte[len];
                    buf[0] = 0x06;
                    buf[1] = 0x01;
                    buf[2] = hwCode;
                    buf[3] = hwSpeed;
                    buf[4] = hwBri;
                    buf[5] = 0x01;
                    buf[6] = 0x00;
                    if (buf.Length >= 39) {
                        for (int i = 0; i < 8; i++) {
                            buf[7 + i * 4 + 0] = color.R;
                            buf[7 + i * 4 + 1] = color.G;
                            buf[7 + i * 4 + 2] = color.B;
                            buf[7 + i * 4 + 3] = 0x00;
                        }
                    }

                    IntPtr h = Hid.Open(dev.Path);
                    if (h != Hid.Invalid && h.ToInt64() > 0) {
                        try {
                            if (Hid.SetFeature(h, buf)) {
                                sent = true;
                                Log.Write(string.Format("CosmicByte sent: fx={0} (code={1}) speed={2} bri={3} rgb=#{4} devLen={5}",
                                    effectIndex, hwCode, hwSpeed, hwBri, color.Hex, dev.FeatureLen));
                            }
                        } catch (Exception ex) {
                            Log.Write("CosmicByte SetFeature error: " + ex.Message);
                        } finally {
                            Hid.CloseHandle(h);
                        }
                    }
                }
            } catch (Exception ex) {
                Log.Write("CosmicByte send: " + ex.Message);
            }
            return sent;
        }

        public static Rgb Frame(int effect, double phase, Rgb color, int index, int totalZones) {
            totalZones = Math.Max(1, totalZones);
            Rgb baseC = (color.R == 0 && color.G == 0 && color.B == 0) ? new Rgb(63, 140, 255) : color;
            switch (effect) {
                case 0: {
                    return baseC;
                }
                case 1: {
                    double pulse = 0.5 + 0.5 * Math.Sin(phase * 1.8);
                    double bri = 0.08 + 0.92 * (pulse * pulse);
                    return baseC.Scale(bri);
                }
                case 2: {
                    double hue = (phase * 35.0 + (index * 360.0 / totalZones)) % 360.0;
                    if (hue < 0) hue += 360.0;
                    return Rgb.FromHue(hue);
                }
                case 3: {
                    double f = Math.Sin(phase * 4.5);
                    return f > 0.15 ? baseC : baseC.Scale(0.1);
                }
                case 4: {
                    double drop = ((phase * 1.5 + (index * 0.37)) % 1.0);
                    if (drop < 0) drop += 1.0;
                    double intensity = drop < 0.25 ? (drop / 0.25) : Math.Max(0.0, 1.0 - (drop - 0.25) / 0.75);
                    return baseC.Scale(0.08 + 0.92 * intensity * intensity);
                }
                case 5: {
                    double cy = (phase * 50.0 + (index * 360.0 / totalZones)) % 360.0;
                    if (cy < 0) cy += 360.0;
                    double b = 0.5 + 0.5 * Math.Sin(cy * Math.PI / 180.0);
                    return baseC.Scale(0.15 + 0.85 * b);
                }
                case 6: {
                    double center = (totalZones - 1) / 2.0;
                    double dist = Math.Abs(index - center) / Math.Max(1.0, center);
                    double wave = Math.Sin((dist * 5.0 - phase * 2.2) * Math.PI);
                    double amp = Math.Max(0.0, wave);
                    return baseC.Scale(0.12 + 0.88 * amp * amp);
                }
                case 7: {
                    double twinkle = Math.Sin(phase * 1.6 + (index * 137.5) * Math.PI / 180.0);
                    if (twinkle > 0.55) {
                        double b = (twinkle - 0.55) / 0.45;
                        return Rgb.Mix(baseC, new Rgb(255, 255, 255), 0.35).Scale(0.2 + 0.8 * b);
                    }
                    return baseC.Scale(0.08);
                }
                case 8: {
                    double trigger = Math.Sin(phase * 2.0 + (index * 5.7) % 3.14);
                    double decay = Math.Max(0.0, trigger);
                    return baseC.Scale(0.06 + 0.94 * Math.Pow(decay, 2));
                }
                case 9: {
                    double head = (phase * 2.5) % totalZones;
                    if (head < 0) head += totalZones;
                    double d = Math.Abs(index - head);
                    if (d > totalZones / 2.0) d = totalZones - d;
                    double s = Math.Max(0.0, 1.0 - d / 2.0);
                    return baseC.Scale(0.08 + 0.92 * s);
                }
                case 10: {
                    double wave = 0.5 + 0.5 * Math.Sin(phase * 2.5 + index * 0.8);
                    Rgb cyan = new Rgb(0, 240, 255);
                    Rgb pink = new Rgb(255, 0, 120);
                    return Rgb.Mix(cyan, pink, wave);
                }
                case 11: {
                    double trigger = Math.Sin(phase * 2.2 + (index * 7.9) % 3.14);
                    double decay = Math.Max(0.0, trigger);
                    return baseC.Scale(0.05 + 0.95 * Math.Pow(decay, 3));
                }
                case 12: {
                    double sw = 0.5 + 0.5 * Math.Sin(phase * 2.0 + (index * 2 * Math.PI / totalZones));
                    return baseC.Scale(0.15 + 0.85 * sw);
                }
                case 13: {
                    double beam = 0.5 + 0.5 * Math.Sin(phase * 3.0);
                    double bPos = beam * totalZones;
                    double dist = Math.Abs(index - bPos);
                    double factor = Math.Max(0.0, 1.0 - dist / 1.5);
                    return baseC.Scale(0.1 + 0.9 * factor);
                }
                case 14: {
                    double wm = (phase * 40.0 + (index * 180.0 / totalZones)) % 180.0;
                    double wmb = 0.5 + 0.5 * Math.Sin(wm * 2 * Math.PI / 180.0);
                    return baseC.Scale(0.2 + 0.8 * wmb);
                }
                case 15: {
                    double wf = ((phase * 2.0 + (index * 0.5)) % 1.0);
                    if (wf < 0) wf += 1.0;
                    double wfi = Math.Max(0.0, 1.0 - wf);
                    return baseC.Scale(0.1 + 0.9 * wfi * wfi);
                }
                case 16: {
                    double center2 = (totalZones - 1) / 2.0;
                    double d2 = Math.Abs(index - center2) / Math.Max(1.0, center2);
                    double p = 0.5 + 0.5 * Math.Sin(phase * 2.5 - d2 * 3.0);
                    return Rgb.Mix(baseC, new Rgb(255, 120, 180), 0.5).Scale(0.15 + 0.85 * p);
                }
                case 17: {
                    double storm = (phase * 60.0 + (index * 360.0 / totalZones)) % 360.0;
                    if (storm < 0) storm += 360.0;
                    double rad = storm * Math.PI / 180.0;
                    double sInt = 0.5 + 0.5 * Math.Cos(rad);
                    return baseC.Scale(0.15 + 0.85 * sInt);
                }
                case 18: {
                    return baseC;
                }
                case 19: {
                    return new Rgb(0, 0, 0);
                }
                default:
                    return baseC;
            }
        }

        public static void PushFrameToDevices(Rgb[] frame) {
            PushFrameToDevices(frame, 100);
        }

        public static void PushFrameToDevices(Rgb[] frame, int intensity) {
            lock (sync) {
                if (cachedDevices == null) return;
                foreach (var dev in cachedDevices) {
                    if (dev.IsLampArray && dev.Device != null) {
                        try {
                            dev.Device.TakeOver(true);
                            if (frame.Length == 1) {
                                dev.Device.SetAll(frame[0], intensity);
                            } else {
                                int[] ids = new int[dev.Device.LampCount];
                                Rgb[] c = new Rgb[dev.Device.LampCount];
                                for (int i = 0; i < ids.Length; i++) {
                                    ids[i] = i;
                                    c[i] = frame[i % frame.Length];
                                }
                                dev.Device.SetLamps(ids, c, intensity);
                            }
                        } catch { }
                    }
                }
            }
        }
    }
}
