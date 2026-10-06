using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int target = 0x0853BB;
        for (int k = 0; k < 80; k++) {
            if (k % 16 == 0) Console.Write(string.Format("\n0x{0:X6}: ", target + k));
            Console.Write(string.Format("{0:X2} ", b[target + k]));
        }
        Console.WriteLine();
    }
}
