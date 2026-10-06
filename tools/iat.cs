using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        // Look at PE headers to find virtual address mapping
        int peOffset = BitConverter.ToInt32(b, 0x3C);
        int imageBase = BitConverter.ToInt32(b, peOffset + 0x34);
        Console.WriteLine(string.Format("ImageBase: 0x{0:X8}", imageBase));
        // Target VA = 0x005CF758 -> offset in file = 0x005CF758 - imageBase + section_offset
        int numSections = BitConverter.ToInt16(b, peOffset + 0x06);
        int optHeaderSize = BitConverter.ToInt16(b, peOffset + 0x14);
        int secTable = peOffset + 0x18 + optHeaderSize;
        int targetVA = 0x005CF758;
        int targetRVA = targetVA - imageBase;
        Console.WriteLine(string.Format("Target RVA: 0x{0:X8}", targetRVA));

        for (int i = 0; i < numSections; i++) {
            int s = secTable + i * 40;
            int virtSize = BitConverter.ToInt32(b, s + 8);
            int virtAddr = BitConverter.ToInt32(b, s + 12);
            int rawSize = BitConverter.ToInt32(b, s + 16);
            int rawAddr = BitConverter.ToInt32(b, s + 20);
            if (targetRVA >= virtAddr && targetRVA < virtAddr + virtSize) {
                int fileOffset = rawAddr + (targetRVA - virtAddr);
                Console.WriteLine(string.Format("Found in section {0} at file offset 0x{1:X8}", i, fileOffset));
                // Read import descriptor or pointer
                int val = BitConverter.ToInt32(b, fileOffset);
                Console.WriteLine(string.Format("Value at 0x{0:X8}: 0x{1:X8}", fileOffset, val));
            }
        }
    }
}
