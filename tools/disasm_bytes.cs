using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int target = 0x0B4180;
        for (int k = 0; k < 100; k++) {
            Console.Write(string.Format("{0:X2} ", b[target + k]));
            if ((k + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine();
    }
}
