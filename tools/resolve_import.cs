using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int peOffset = BitConverter.ToInt32(b, 0x3C);
        int numSections = BitConverter.ToInt16(b, peOffset + 0x06);
        int optHeaderSize = BitConverter.ToInt16(b, peOffset + 0x14);
        int secTable = peOffset + 0x18 + optHeaderSize;
        int rva = 0x00232A4E;
        for (int i = 0; i < numSections; i++) {
            int s = secTable + i * 40;
            int virtSize = BitConverter.ToInt32(b, s + 8);
            int virtAddr = BitConverter.ToInt32(b, s + 12);
            int rawSize = BitConverter.ToInt32(b, s + 16);
            int rawAddr = BitConverter.ToInt32(b, s + 20);
            if (rva >= virtAddr && rva < virtAddr + virtSize) {
                int fileOffset = rawAddr + (rva - virtAddr);
                // Hint/Name table entry: 2 bytes hint, then ASCII string
                int nameOffset = fileOffset + 2;
                int len = 0;
                while (b[nameOffset + len] != 0) len++;
                string name = Encoding.ASCII.GetString(b, nameOffset, len);
                Console.WriteLine(string.Format("Import name at RVA 0x{0:X8} (file offset 0x{1:X8}): {2}", rva, fileOffset, name));
            }
        }
    }
}
