using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int off1 = 0x02B4B0; // 0x0042C0B0
        int off2 = 0x002DC0; // 0x004039C0
        Console.WriteLine("--- 0x0042C0B0 ---");
        for (int i = 0; i < 64; i++) {
            if (i % 16 == 0) Console.Write(string.Format("\n0x{0:X6}: ", off1 + i));
            Console.Write(string.Format("{0:X2} ", b[off1 + i]));
        }
        Console.WriteLine("\n\n--- 0x004039C0 ---");
        for (int i = 0; i < 64; i++) {
            if (i % 16 == 0) Console.Write(string.Format("\n0x{0:X6}: ", off2 + i));
            Console.Write(string.Format("{0:X2} ", b[off2 + i]));
        }
        Console.WriteLine();
    }
}
