using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        int peOffset = BitConverter.ToInt32(b, 0x3C);
        int numSections = BitConverter.ToInt16(b, peOffset + 0x06);
        int optHeaderSize = BitConverter.ToInt16(b, peOffset + 0x14);
        int secTable = peOffset + 0x18 + optHeaderSize;
        int targetRVA = 0x005CF220 - 0x00400000;
        for (int i = 0; i < numSections; i++) {
            int s = secTable + i * 40;
            int virtAddr = BitConverter.ToInt32(b, s + 12);
            int virtSize = BitConverter.ToInt32(b, s + 8);
            int rawAddr = BitConverter.ToInt32(b, s + 20);
            if (targetRVA >= virtAddr && targetRVA < virtAddr + virtSize) {
                int fileOffset = rawAddr + (targetRVA - virtAddr);
                int hintRVA = BitConverter.ToInt32(b, fileOffset);
                Console.WriteLine(string.Format("Hint RVA at 0x{0:X8}: 0x{1:X8}", fileOffset, hintRVA));
                // Find hint in file
                for (int k = 0; k < numSections; k++) {
                    int s2 = secTable + k * 40;
                    int vA2 = BitConverter.ToInt32(b, s2 + 12);
                    int vS2 = BitConverter.ToInt32(b, s2 + 8);
                    int rA2 = BitConverter.ToInt32(b, s2 + 20);
                    if (hintRVA >= vA2 && hintRVA < vA2 + vS2) {
                        int fOff2 = rA2 + (hintRVA - vA2);
                        int len = 0;
                        while (b[fOff2 + 2 + len] != 0) len++;
                        string name = System.Text.Encoding.ASCII.GetString(b, fOff2 + 2, len);
                        Console.WriteLine("API Name: " + name);
                    }
                }
            }
        }
    }
}
