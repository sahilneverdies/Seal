using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        Console.WriteLine("String at 0x1F9C10: " + Encoding.Unicode.GetString(b, 0x1F9C10, 100));
        Console.WriteLine("String at 0x2052B4: " + Encoding.Unicode.GetString(b, 0x2052B4, 100));
    }
}
