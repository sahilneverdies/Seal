using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        SearchPattern(b, "tc_kb_led");
    }

    static void SearchPattern(byte[] b, string str) {
        byte[] p = System.Text.Encoding.Unicode.GetBytes(str);
        for (int i = 0; i < b.Length - p.Length; i++) {
            bool ok = true;
            for (int j = 0; j < p.Length; j++) if (b[i + j] != p[j]) { ok = false; break; }
            if (ok) {
                Console.WriteLine(string.Format("Found '{0}' at offset 0x{1:X6}", str, i));
            }
        }
    }
}
