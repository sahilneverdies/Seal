using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int rva = 0x00232A4E;
        // Search for where this string is in file
        for (int i = 0; i < b.Length - 20; i++) {
            if (b[i] == 'H' && b[i+1] == 'i' && b[i+2] == 'd' && b[i+3] == 'D' && b[i+4] == '_') {
                int len = 0;
                while (b[i + len] != 0) len++;
                Console.WriteLine(string.Format("0x{0:X6}: {1}", i, Encoding.ASCII.GetString(b, i, len)));
            }
        }
    }
}
