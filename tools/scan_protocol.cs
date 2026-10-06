using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        string dllPath = @"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\InitSetup.dll";
        string exePath = @"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe";

        Console.WriteLine("Scanning InitSetup.dll...");
        Scan(dllPath);

        Console.WriteLine("Scanning OemDrv.exe...");
        Scan(exePath);
    }

    static void Scan(string path) {
        if (!File.Exists(path)) return;
        byte[] bytes = File.ReadAllBytes(path);

        // Find strings
        var sb = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++) {
            byte b = bytes[i];
            if (b >= 32 && b <= 126) sb.Append((char)b);
            else {
                if (sb.Length >= 4) {
                    string s = sb.ToString();
                    if (s.IndexOf("hid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("led", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("report", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("feature", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("write", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("send", StringComparison.OrdinalIgnoreCase) >= 0) {
                        Console.WriteLine("  STR: " + s);
                    }
                }
                sb.Length = 0;
            }
        }
    }
}
