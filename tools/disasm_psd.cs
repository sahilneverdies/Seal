using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int callSite = 0x00BA52 - 0x2A; // find where E8 A6 9A 07 00 is
        for (int i = 0x00BA00; i < 0x00BA60; i++) {
            if (b[i] == 0xE8 && b[i+1] == 0xA6 && b[i+2] == 0x9A && b[i+3] == 0x07 && b[i+4] == 0x00) {
                int target = i + 5 + BitConverter.ToInt32(b, i + 1);
                Console.WriteLine(string.Format("Call at 0x{0:X6} -> target 0x{1:X6}", i, target));
                // Dump target function
                for (int k = 0; k < 120; k++) {
                    if (k % 16 == 0) Console.Write(string.Format("\n0x{0:X6}: ", target + k));
                    Console.Write(string.Format("{0:X2} ", b[target + k]));
                }
                Console.WriteLine();
                break;
            }
        }
    }
}
