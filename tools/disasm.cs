using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int offset = 0x0AE480;
        int len = 300;
        Console.WriteLine(string.Format("Dumping bytes at 0x{0:X6}:", offset));
        for (int i = 0; i < len; i++) {
            if (i % 16 == 0) Console.Write(string.Format("\n0x{0:X6}: ", offset + i));
            Console.Write(string.Format("{0:X2} ", b[offset + i]));
        }
        Console.WriteLine();
    }
}
