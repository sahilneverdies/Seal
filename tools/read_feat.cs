using System;
using System.IO;
using System.Runtime.InteropServices;

class Program {
    [DllImport("hid.dll")] static extern bool HidD_GetFeature(IntPtr h, byte[] buf, int len);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(string name, uint acc, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static void Main() {
        string path = @"\\?\hid#vid_b6a4&pid_4091&mi_02&col04#7&21d26f1d&0&0003#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        IntPtr h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.ToInt64() <= 0) h = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        Console.WriteLine("Handle: " + h);
        if (h.ToInt64() > 0) {
            byte[] buf = new byte[41];
            for (byte id = 0; id <= 8; id++) {
                buf[0] = id;
                bool ok = HidD_GetFeature(h, buf, buf.Length);
                Console.WriteLine(string.Format("GetFeature(id={0}): {1}", id, ok));
                if (ok) {
                    Console.WriteLine("  " + BitConverter.ToString(buf));
                }
            }
            CloseHandle(h);
        }
    }
}
