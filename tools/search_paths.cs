using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        // Search for string "\\\\?\\" or "HID#" or "\\?\hid#"
        SearchString(b, "\\\\?\\");
        SearchString(b, "HID#");
        SearchString(b, "hid#");
        SearchString(b, "VID_");
        SearchString(b, "vid_");
    }

    static void SearchString(byte[] b, string str) {
        Console.WriteLine("Search: " + str);
        byte[] a = System.Text.Encoding.ASCII.GetBytes(str);
        byte[] u = System.Text.Encoding.Unicode.GetBytes(str);
        FindPattern(b, a, "ASCII");
        FindPattern(b, u, "Unicode");
    }

    static void FindPattern(byte[] b, byte[] p, string tag) {
        for (int i = 0; i < b.Length - p.Length; i++) {
            bool ok = true;
            for (int j = 0; j < p.Length; j++) if (b[i + j] != p[j]) { ok = false; break; }
            if (ok) Console.WriteLine(string.Format("  [{0}] at offset 0x{1:X6}", tag, i));
        }
    }
}
