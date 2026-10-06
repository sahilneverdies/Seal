using System;
using System.IO;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\InitSetup.dll");
        int peOffset = BitConverter.ToInt32(b, 0x3C);
        int optHeaderOffset = peOffset + 0x18;
        int exportRVA = BitConverter.ToInt32(b, optHeaderOffset + 0x60);
        Console.WriteLine(string.Format("Export RVA: 0x{0:X8}", exportRVA));
        // Find exports in file
        int numSections = BitConverter.ToInt16(b, peOffset + 0x06);
        int secTable = peOffset + 0x18 + BitConverter.ToInt16(b, peOffset + 0x14);
        for (int i = 0; i < numSections; i++) {
            int s = secTable + i * 40;
            int virtAddr = BitConverter.ToInt32(b, s + 12);
            int virtSize = BitConverter.ToInt32(b, s + 8);
            int rawAddr = BitConverter.ToInt32(b, s + 20);
            if (exportRVA >= virtAddr && exportRVA < virtAddr + virtSize) {
                int expOff = rawAddr + (exportRVA - virtAddr);
                int numNames = BitConverter.ToInt32(b, expOff + 24);
                int namesRVA = BitConverter.ToInt32(b, expOff + 32);
                int namesOff = rawAddr + (namesRVA - virtAddr);
                Console.WriteLine("Exported functions count: " + numNames);
                for (int k = 0; k < numNames; k++) {
                    int nameRVA = BitConverter.ToInt32(b, namesOff + k * 4);
                    int nameOff = rawAddr + (nameRVA - virtAddr);
                    int len = 0;
                    while (b[nameOff + len] != 0) len++;
                    Console.WriteLine("  " + System.Text.Encoding.ASCII.GetString(b, nameOff, len));
                }
            }
        }
    }
}
