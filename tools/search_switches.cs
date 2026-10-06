using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        byte[] b = File.ReadAllBytes(@"C:\Program Files (x86)\Cosmic Byte\Artemis Wireless\OemDrv.exe");
        string s = System.Text.Encoding.ASCII.GetString(b);
        var r = new Regex(@"[-/][a-zA-Z0-9_]+");
        var m = r.Matches(s);
        var set = new System.Collections.Generic.HashSet<string>();
        foreach (Match x in m) {
            if (x.Value.Length > 2 && set.Add(x.Value)) {
                Console.WriteLine("  " + x.Value);
            }
        }
    }
}
