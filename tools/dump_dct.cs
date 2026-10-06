using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Users\1nOnlySahil\AppData\Local\BYCOMBO4\Artemis Wireless Keyboard\profile.dct");
        for (int i = 0; i < b.Length; i += 64) {
            bool hasNonZero = false;
            for (int k = 0; k < 64 && i + k < b.Length; k++) {
                if (b[i + k] != 0) { hasNonZero = true; break; }
            }
            if (hasNonZero && i >= 0x400 && i <= 0x1000) {
                Console.Write(string.Format("0x{0:X4}: ", i));
                for (int k = 0; k < 64 && i + k < b.Length; k++) {
                    Console.Write(string.Format("{0:X2} ", b[i + k]));
                }
                Console.WriteLine();
            }
        }
    }
}
