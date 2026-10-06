using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int fileOffset = 0x1FD628;
        string s = Encoding.Unicode.GetString(b, fileOffset, 40);
        Console.WriteLine("String at 0x1FD628: " + s);
    }
}
