using System;
using System.IO;

class Program {
    static void Main() {
        string exe = @"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe";
        byte[] b = File.ReadAllBytes(exe);

        // Find type descriptor for CLedDlg
        // 0x63B908 - 8 is typically the type descriptor address
        uint td = 0x63B900;
        byte b1 = (byte)(td & 0xFF);
        byte b2 = (byte)((td >> 8) & 0xFF);
        byte b3 = (byte)((td >> 16) & 0xFF);
        byte b4 = (byte)((td >> 24) & 0xFF);

        for (int i = 0; i < b.Length - 4; i++) {
            if (b[i] == b1 && b[i + 1] == b2 && b[i + 2] == b3 && b[i + 3] == b4) {
                Console.WriteLine("Type descriptor ref at file offset: 0x" + i.ToString("X"));
            }
        }
    }
}
