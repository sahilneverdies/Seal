using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int start = 0x1F9A00;
        int end = 0x1FA200;
        for (int i = start; i < end - 4; i += 2) {
            if (b[i] >= 32 && b[i] <= 126 && b[i + 1] == 0) {
                int len = 0;
                while (i + len * 2 + 1 < b.Length && b[i + len * 2] >= 32 && b[i + len * 2] <= 126 && b[i + len * 2 + 1] == 0) len++;
                if (len >= 3) {
                    Console.WriteLine(string.Format("0x{0:X6}: {1}", i, Encoding.Unicode.GetString(b, i, len * 2)));
                    i += len * 2;
                }
            }
        }
    }
}
