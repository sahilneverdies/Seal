using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Users\1nOnlySahil\AppData\Local\BYCOMBO4\Artemis Wireless Keyboard\profile.dct");
        int start = 0x2F00;
        int len = 256;
        for (int i = 0; i < len; i++) {
            if (i % 16 == 0) Console.Write(string.Format("\n0x{0:X4}: ", start + i));
            Console.Write(string.Format("{0:X2} ", b[start + i]));
        }
        Console.WriteLine();
    }
}
