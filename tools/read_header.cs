using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Users\1nOnlySahil\AppData\Local\BYCOMBO4\Artemis Wireless Keyboard\profile.dct");
        // Look at the first 0x400 bytes (header of profile)
        Console.WriteLine("Header bytes of profile.dct:");
        for (int i = 0; i < 0x80; i++) {
            Console.Write(string.Format("{0:X2}:{1:X2} ", i, b[i]));
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine();
    }
}
