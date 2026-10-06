using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        // Search for patterns like: mov byte ptr [esi/edi/ebx], 6 or push 41
        // Look for 0x29 (41 in hex)
        Console.WriteLine("Searching for 0x29 (41 bytes)...");
        for (int i = 0; i < b.Length - 10; i++) {
            // push 0x29 (6A 29)
            if (b[i] == 0x6A && b[i + 1] == 0x29) {
                Console.WriteLine(string.Format("push 41 at 0x{0:X6}", i));
                for (int k = Math.Max(0, i - 40); k < Math.Min(b.Length, i + 40); k++) {
                    Console.Write(string.Format("{0:X2} ", b[k]));
                }
                Console.WriteLine("\n");
            }
        }
    }
}
