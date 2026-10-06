using System;
using System.Collections.Generic;
using Seal;

class Program {
    static void Main() {
        var devices = ExternalKeyboards.Detect(true);
        Console.WriteLine("Detected external keyboards: " + devices.Count);
        foreach (var d in devices) {
            Console.WriteLine(string.Format("Name='{0}' Brand='{1}' VID=0x{2:X4} PID=0x{3:X4} Path='{4}' IsLamp={5}", d.Name, d.Brand, d.VendorId, d.ProductId, d.Path, d.IsLampArray));
        }
    }
}
