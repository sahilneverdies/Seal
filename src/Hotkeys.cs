

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace Seal {

    public enum HotkeyAction { Eco = 0, Balanced = 1, Performance = 2, MaxFan = 3, Cycle = 4 }


    public struct Hotkey {
        public const uint Alt = 1, Ctrl = 2, Shift = 4, Win = 8;
        public uint Mods, Vk;
        public Hotkey(uint mods, uint vk) { Mods = mods; Vk = vk; }
        public static readonly Hotkey None = new Hotkey();
        public bool IsEmpty { get { return Vk == 0; } }
        public bool Same(Hotkey o) { return Mods == o.Mods && Vk == o.Vk; }


        public bool Valid { get { return Vk != 0 && (Mods != 0 || IsFunctionKey(Vk)); } }
        static bool IsFunctionKey(uint vk) { return vk >= 0x70 && vk <= 0x87; }

        public bool TypesCharacter(IntPtr layout) {
            if ((Mods & (Ctrl | Alt)) != (Ctrl | Alt) || (Mods & Win) != 0 || Vk == 0) return false;
            try {
                var state = new byte[256];
                state[0x11] = state[0xA2] = state[0x12] = state[0xA5] = 0x80;
                if ((Mods & Shift) != 0) state[0x10] = state[0xA0] = 0x80;
                var buf = new StringBuilder(8);

                int r = ToUnicodeEx(Vk, MapVirtualKeyEx(Vk, 0, layout), state, buf, buf.Capacity, 4, layout);
                return r < 0 || (r > 0 && buf.Length > 0 && buf[0] >= 0x20);
            } catch { return false; }
        }
        [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint code, uint type, IntPtr hkl);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, [Out] StringBuilder buf, int size, uint flags, IntPtr hkl);

        public override string ToString() {
            if (IsEmpty) return "";
            var sb = new StringBuilder();
            if ((Mods & Ctrl) != 0) sb.Append("Ctrl+");
            if ((Mods & Alt) != 0) sb.Append("Alt+");
            if ((Mods & Shift) != 0) sb.Append("Shift+");
            if ((Mods & Win) != 0) sb.Append("Win+");
            sb.Append(KeyName(Vk));
            return sb.ToString();
        }

        public string[] Parts() { return IsEmpty ? new string[0] : ToString().Split('+'); }

        public static string KeyName(uint vk) {
            if ((vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9')) return ((char)vk).ToString();
            if (IsFunctionKey(vk)) return "F" + (vk - 0x6F);
            switch (vk) {
                case 0x20: return "Space"; case 0x1B: return "Esc"; case 0x0D: return "Enter"; case 0x09: return "Tab";
                case 0x21: return "PageUp"; case 0x22: return "PageDown"; case 0x23: return "End"; case 0x24: return "Home";
                case 0x25: return "Left"; case 0x26: return "Up"; case 0x27: return "Right"; case 0x28: return "Down";
                case 0x2D: return "Insert"; case 0x2E: return "Delete"; case 0x2C: return "PrintScreen"; case 0x91: return "ScrollLock"; case 0x13: return "Pause";
            }
            try { return KeyInterop.KeyFromVirtualKey((int)vk).ToString(); } catch { return "0x" + vk.ToString("X2"); }
        }

        public static bool TryParse(string s, out Hotkey h) {
            h = None;
            if (string.IsNullOrEmpty(s)) return true;
            uint mods = 0, vk = 0;
            foreach (string raw in s.Split('+')) {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                string u = t.ToUpperInvariant();
                if (u == "CTRL" || u == "CONTROL") mods |= Ctrl;
                else if (u == "ALT") mods |= Alt;
                else if (u == "SHIFT") mods |= Shift;
                else if (u == "WIN" || u == "WINDOWS") mods |= Win;
                else if (u.Length == 1 && (char.IsLetterOrDigit(u[0]))) vk = u[0];
                else if (u.Length >= 2 && u[0] == 'F' && char.IsDigit(u[1])) { int n; if (int.TryParse(u.Substring(1), out n) && n >= 1 && n <= 24) vk = (uint)(0x6F + n); }
                else {

                    for (uint c = 0; c < 256; c++) if (KeyName(c).Equals(t, StringComparison.OrdinalIgnoreCase)) { vk = c; break; }
                    if (vk == 0) { Key k; if (Enum.TryParse<Key>(t, true, out k)) vk = (uint)KeyInterop.VirtualKeyFromKey(k); }
                }
            }
            if (vk == 0) return false;
            h = new Hotkey(mods, vk);
            return true;
        }

        public static bool FromKey(Key key, ModifierKeys held, out Hotkey h) {
            h = None;
            switch (key) {
                case Key.LeftCtrl: case Key.RightCtrl: case Key.LeftAlt: case Key.RightAlt:
                case Key.LeftShift: case Key.RightShift: case Key.LWin: case Key.RWin: case Key.None:
                    return false;
            }
            uint mods = 0;
            if ((held & ModifierKeys.Control) != 0) mods |= Ctrl;
            if ((held & ModifierKeys.Alt) != 0) mods |= Alt;
            if ((held & ModifierKeys.Shift) != 0) mods |= Shift;
            if ((held & ModifierKeys.Windows) != 0) mods |= Win;
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk <= 0) return false;
            h = new Hotkey(mods, (uint)vk);
            return true;
        }
    }

    public static class HotkeyTable {
        public const int Count = 5;
        public static readonly string[] Names = { "Eco", "Balanced", "Performance", "Max fan", "Cycle modes" };
        public static readonly string[] Keys = { "Eco", "Balanced", "Performance", "MaxFan", "Cycle" };
        public static readonly Hotkey[] Defaults = {
            CtrlAlt('E'), CtrlAlt('B'), CtrlAlt('P'), CtrlAlt('M'),
            new Hotkey(Hotkey.Shift, 0x7A),
        };
        static Hotkey CtrlAlt(char c) { return new Hotkey(Hotkey.Ctrl | Hotkey.Alt, (uint)c); }

        public static string Summary(Hotkey[] b, string omenKey) {
            var order = new System.Collections.Generic.List<uint>();
            var items = new System.Collections.Generic.Dictionary<uint, System.Collections.Generic.List<string>>();
            Action<uint, string> add = delegate(uint mods, string text) {
                if (!items.ContainsKey(mods)) { items[mods] = new System.Collections.Generic.List<string>(); order.Add(mods); }
                items[mods].Add(text);
            };
            Hotkey e = b[0], m = b[1], p = b[2];
            bool folded = !e.IsEmpty && !m.IsEmpty && !p.IsEmpty && e.Mods == m.Mods && m.Mods == p.Mods
                && Hotkey.KeyName(e.Vk).Length == 1 && Hotkey.KeyName(m.Vk).Length == 1 && Hotkey.KeyName(p.Vk).Length == 1;
            if (folded) add(e.Mods, Hotkey.KeyName(e.Vk) + "/" + Hotkey.KeyName(m.Vk) + "/" + Hotkey.KeyName(p.Vk) + " modes");
            else {
                if (!e.IsEmpty) add(e.Mods, Hotkey.KeyName(e.Vk) + " eco");
                if (!m.IsEmpty) add(m.Mods, Hotkey.KeyName(m.Vk) + " balanced");
                if (!p.IsEmpty) add(p.Mods, Hotkey.KeyName(p.Vk) + " performance");
            }
            if (!b[3].IsEmpty) add(b[3].Mods, Hotkey.KeyName(b[3].Vk) + " max fan");
            if (!b[4].IsEmpty) add(b[4].Mods, Hotkey.KeyName(b[4].Vk) + " cycles");
            if (omenKey != null) add(uint.MaxValue, omenKey);
            if (order.Count == 0) return "No shortcuts set";
            var parts = new System.Collections.Generic.List<string>();
            foreach (uint mods in order) {
                if (mods == uint.MaxValue) { parts.Add("Fn+F12 " + items[mods][0]); continue; }
                string prefix = new Hotkey(mods, 'X').ToString();
                prefix = prefix.Substring(0, prefix.Length - 1);
                string list = string.Join(", ", items[mods].ToArray());

                parts.Add(prefix.Length == 0 ? list : items[mods].Count == 1 ? prefix + list : prefix.Substring(0, prefix.Length - 1) + ": " + list);
            }
            return string.Join(" · ", parts.ToArray());
        }
    }
}
