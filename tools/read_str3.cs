using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int rva = 0x005E5EC4 - 0x00400000;
        int fileOffset = rva - 0x1E0000 + 0x1DF200; // let's find section
        for (int i = 0; i < b.Length - 10; i++) {
            // Find string or check
        }
        // Let's resolve VA 0x005E5EC4
        int peOffset = BitConverter.ToInt32(b, 0x3C);
        int numSections = BitConverter.ToInt16(b, peOffset + 0x06);
        int optHeaderSize = BitConverter.ToInt16(b, peOffset + 0x14);
        int secTable = peOffset + 0x18 + optHeaderSize;
        for (int i = 0; i < numSections; i++) {
            int s = secTable + i * 40;
            int virtAddr = BitConverter.ToInt32(b, s + 12);
            int virtSize = BitConverter.ToInt32(b, s + 8);
            int rawAddr = BitConverter.ToInt32(b, s + 20);
            if (rva >= virtAddr && rva < virtAddr + virtSize) {
                int off = rawAddr + (rva - virtAddr);
                Console.WriteLine("File offset: 0x" + off.ToString("X6"));
                int len = 0;
                while (b[off + len] != 0) len++;
                Console.WriteLine("ASCII: " + System.Text.Encoding.ASCII.GetString(b, off, len));
                Console.WriteLine("Unicode: " + System.Text.Encoding.Unicode.GetString(b, off, len * 2));
            }
        }
    }
}
