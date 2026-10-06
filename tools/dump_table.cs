using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int peOffset = BitConverter.ToInt32(b, 0x3C);
        int numSections = BitConverter.ToInt16(b, peOffset + 0x06);
        int optHeaderSize = BitConverter.ToInt16(b, peOffset + 0x14);
        int secTable = peOffset + 0x18 + optHeaderSize;
        int targetRVA = 0x00255D64;
        int fileOffset = 0;
        for (int i = 0; i < numSections; i++) {
            int s = secTable + i * 40;
            string name = System.Text.Encoding.ASCII.GetString(b, s, 8).TrimEnd('\0');
            int virtAddr = BitConverter.ToInt32(b, s + 12);
            int rawSize = BitConverter.ToInt32(b, s + 16);
            int rawAddr = BitConverter.ToInt32(b, s + 20);
            Console.WriteLine(string.Format("Sec {0}: {1} RVA 0x{2:X6} RawSize 0x{3:X6} RawAddr 0x{4:X6}", i, name, virtAddr, rawSize, rawAddr));
            if (targetRVA >= virtAddr && targetRVA < virtAddr + Math.Max(rawSize, BitConverter.ToInt32(b, s + 8))) {
                fileOffset = rawAddr + (targetRVA - virtAddr);
            }
        }
        Console.WriteLine(string.Format("fileOffset: 0x{0:X6}", fileOffset));
        if (fileOffset > 0) {
            for (int k = 0; k < 164; k += 4) {
                int val = BitConverter.ToInt32(b, fileOffset + k);
                Console.WriteLine(string.Format("Item {0}: {1} (0x{1:X2})", k / 4, val));
            }
        }
    }
}
