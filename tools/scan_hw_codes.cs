using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        Dump(b, 0x1F4990, 80, "0x1F4990");
        Dump(b, 0x1F7120, 80, "0x1F7120");
        Dump(b, 0x1F72A0, 80, "0x1F72A0");
    }

    static void Dump(byte[] b, int start, int count, string label) {
        Console.WriteLine("--- " + label + " ---");
        for (int i = 0; i < count; i++) {
            if (i % 16 == 0) Console.Write(string.Format("\n0x{0:X6}: ", start + i));
            Console.Write(string.Format("{0:X2} ", b[start + i]));
        }
        Console.WriteLine("\n");
    }
}
