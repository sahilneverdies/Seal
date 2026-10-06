

using System;
using System.Security.Principal;
using System.Threading;
using System.Windows;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

[assembly: System.Reflection.AssemblyTitle("Seal")]
[assembly: System.Reflection.AssemblyDescription("Fan, thermal and keyboard lighting control for HP OMEN and Victus laptops.")]

namespace Seal {
    public static class Program {


        public const string AppName = "Seal";
        public static string DisplayName = AppName;
        public const string Version = Meta.Version;
        public static string FileStem { get { return AppName.ToLowerInvariant(); } }
        public static EventWaitHandle ShowEvent, ExitEvent;
        public static bool JustUpdated;
        public static bool FlashTest;
        public static bool KeyboardTest;
        public static string StartPage = "";
        public static bool FanProbe;

        [STAThread]
        public static int Main(string[] args) {
            bool demo = false, hidden = false, settings = false;
            string shot = null;
            var overrides = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++) {
                string a = args[i].ToLowerInvariant();
                if (a == "--demo") demo = true;
                else if (a == "--hidden") hidden = true;
                else if (a == "--settings") settings = true;
                else if (a == "--screenshot" && i + 1 < args.Length) shot = args[++i];
                else if (a == "--set" && i + 1 < args.Length) overrides.Add(args[++i]);
                else if (a == "--flash") FlashTest = true;
                else if (a == "--keyboard") KeyboardTest = true;
                else if (a == "--page" && i + 1 < args.Length) StartPage = args[++i].ToLowerInvariant();
                else if (a == "--board" && i + 1 < args.Length) Platforms.BoardOverride = args[++i];
            }
            foreach (string a0 in args) if (a0.ToLowerInvariant() == "--lamps") return ListLamps();


            foreach (string a0 in args) if (a0.ToLowerInvariant() == "--support") return WriteSupport(args);
            foreach (string a0 in args) if (a0.ToLowerInvariant() == "--fantest") FanProbe = true;
            foreach (string a0 in args) if (a0.ToLowerInvariant() == "--driver") return WriteDriverReport(args);
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i].ToLowerInvariant() == "--make-ico") { MainWindow.WriteIco(args[i + 1], Ui.BalColor); return 0; }
            bool wantExit = false;
            foreach (string a0 in args) if (a0.ToLowerInvariant() == "--exit") wantExit = true;
            foreach (string a0 in args) if (a0.ToLowerInvariant() == "--updated") JustUpdated = true;
            bool created;
            if (shot != null) demo = true;

            string instance = AppName + (demo ? "_Demo" : "");
            var mutex = new Mutex(true, instance + "_SingleInstance", out created);
            if (!created && shot == null) {

                if (JustUpdated) {
                    try { created = mutex.WaitOne(15000); }
                    catch (AbandonedMutexException) { created = true; }
                    catch { }
                }
                if (!created) {
                    try { EventWaitHandle.OpenExisting(instance + (wantExit ? "_Exit" : "_ShowPanel")).Set(); } catch { }
                    return 0;
                }
            }
            if (wantExit) return 0;
            try { ShowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instance + "_ShowPanel"); } catch { }
            try { ExitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instance + "_Exit"); } catch { }

            bool elevated = false;
            try { elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { }
            IHardware hw = HardwareFactory.Create(demo, elevated);
            Log.Write("---- " + AppName + " " + Version + " start · elevated=" + elevated + " · hardware=" + (hw.IsDemo ? "demo" : hw.GetType().Name) + (hidden ? " · hidden" : ""));


            if (JustUpdated) new Thread(Update.CleanOld) { IsBackground = true, Name = "update-cleanup" }.Start();

            var settingsObj = Settings.Load();
            foreach (string o in overrides) { int eq = o.IndexOf('='); if (eq > 0) settingsObj.Apply(o.Substring(0, eq), o.Substring(eq + 1)); }
            if (overrides.Count > 0 || shot != null) settingsObj.NoPersist = true;
            if (settingsObj.StartHidden) hidden = true;
            if (!string.IsNullOrEmpty(settingsObj.Name)) DisplayName = settingsObj.Name.Trim();
            var engine = new Engine(hw, settingsObj);
            engine.Init();
            var sensors = new Sensors();
            sensors.CpuSource = delegate { return engine.Cpu; };
            sensors.Start();

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            AppDomain.CurrentDomain.UnhandledException += delegate(object o, UnhandledExceptionEventArgs e) { Log.Write("UNHANDLED: " + e.ExceptionObject); };
            app.DispatcherUnhandledException += delegate(object o, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { Log.Write("UI EXCEPTION: " + e.Exception); e.Handled = true; };

            MainWindow win;
            try { win = new MainWindow(engine, sensors, shot, settings); }
            catch (Exception ex) {
                Log.Write("window init failed: " + ex);
                MessageBox.Show(AppName + " could not build its window:\n\n" + ex.Message + "\n\nSee " + FileStem + ".log.", AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
                return 1;
            }
            if (!hidden || shot != null) win.Show();
            app.Run();
            GC.KeepAlive(mutex);
            return 0;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool AllocConsole();
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int which);

        static bool OpenConsole() {
            try {
                if (AttachConsole(-1)) { PointStdOutAtConsole(); return false; }
                IntPtr h = GetStdHandle(-11);
                bool nobodyListening = h == IntPtr.Zero || h == new IntPtr(-1);
                if (nobodyListening && AllocConsole()) { PointStdOutAtConsole(); return true; }
            } catch { }
            return false;
        }

        static void PointStdOutAtConsole() {
            var w = new System.IO.StreamWriter(Console.OpenStandardOutput());
            w.AutoFlush = true;
            Console.SetOut(w);
        }

        static void HoldConsole(bool ours) {
            if (!ours) return;
            try {
                Console.WriteLine();
                Console.WriteLine("Press Enter to close.");
                Console.SetIn(new System.IO.StreamReader(Console.OpenStandardInput()));
                Console.ReadLine();
            } catch { }
        }

        static int WriteSupport(string[] args) { return WriteReport(args, "support-info.txt", Support.Report, "the firmware was not asked anything"); }
        static int WriteDriverReport(string[] args) { return WriteReport(args, "driver-check.txt", Support.DriverReport, "the driver and the firmware were not asked anything"); }

        static int WriteReport(string[] args, string fileName, Func<Engine, string> build, string notElevated) {
            bool ownConsole = OpenConsole();
            bool elev = false;
            try { elev = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { }
            bool wantDemo = false;
            foreach (string a in args) if (a.ToLowerInvariant() == "--demo") wantDemo = true;
            IHardware hw2 = HardwareFactory.Create(wantDemo, elev);
            if (!elev) Console.WriteLine("NOT ELEVATED - " + notElevated + ". Run this from an administrator prompt.\n");
            var s2 = Settings.Load();
            s2.NoPersist = true;
            var eng = new Engine(hw2, s2);
            try { eng.Init(false); } catch (Exception ex) { Console.WriteLine("engine init failed: " + ex.Message); }
            string rep;
            try { rep = build(eng); } catch (Exception ex) { rep = "report failed: " + ex; }
            Console.WriteLine(rep);


            if (eng.EcPairFound != 0) {
                try { System.IO.File.WriteAllText(Engine.EcPairPath, eng.EcPairFound == 2 ? "percent" : "rpm"); Console.WriteLine("recorded which fan register pair this board uses; Seal reads it at its next start"); }
                catch (Exception ex) { Console.WriteLine("could not record the fan register pair: " + ex.Message); }
            }
            try {
                string p = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Log.Path), fileName);
                System.IO.File.WriteAllText(p, rep);
                Console.WriteLine("saved to " + p);
            } catch (Exception ex) { Console.WriteLine("could not save: " + ex.Message); }
            try { eng.Dispose(); } catch { }
            HoldConsole(ownConsole);
            return 0;
        }

        static int ListLamps() {
            bool ownConsole = OpenConsole();
            try { return ListLampsBody(); } finally { HoldConsole(ownConsole); }
        }

        static int ListLampsBody() {


            Action<string> say = delegate(string s) { Console.WriteLine(s); Log.Write("lamps| " + s); };
            var all = Hid.Enumerate();
            int lighting = 0;
            say(AppName + " " + Version + ": HID lighting devices");
            say(all.Count + " HID collections present");
            foreach (var info in all) {
                if (info.UsagePage != LampArray.UsagePageLighting) continue;
                lighting++;
                say("");
                say("  " + info);
                say("  " + info.Path);
            }
            if (lighting == 0) { say(""); say("No HID Lighting And Illumination collection (usage page 0x59) on this machine."); return 0; }
            foreach (var la in LampArray.All()) {
                say("");
                say("LampArray VID_" + la.VendorId.ToString("X4") + " PID_" + la.ProductId.ToString("X4") + "  " + la.Product);
                say("  " + la.Describe);
                say("  usable for per-key painting: " + (la.UsableAsPerKey ? "yes" : "no"));
                say("  lamp   x(mm)   y(mm)  prog  key usage");
                for (int i = 0; i < la.LampCount; i++)
                    say("  " + i.ToString().PadLeft(4) + "  " + (la.X[i] / 1000.0).ToString("0.0").PadLeft(6) +
                        "  " + (la.Y[i] / 1000.0).ToString("0.0").PadLeft(6) +
                        "  " + (la.Programmable(i) ? " yes" : "  no") + "   0x" + la.KeyUsage[i].ToString("X2"));
                la.Dispose();
            }
            return 0;
        }
    }
}

