

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Seal {


    public sealed class PawnIoModule : IDisposable {
        const uint DeviceType = 41394u << 16;
        const uint IoctlLoad = DeviceType | (0x821u << 2);
        const uint IoctlExecute = DeviceType | (0x841u << 2);
        const int NameLength = 32;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuf, uint inLen, byte[] outBuf, uint outLen, out uint returned, IntPtr overlapped);

        readonly SafeFileHandle handle;
        readonly object sync = new object();
        public readonly string Name;

        PawnIoModule(SafeFileHandle h, string name) { handle = h; Name = name; }

        public static PawnIoModule Load(string name, byte[] blob, out string why) { bool absent; return Load(name, blob, out why, out absent); }

        public static PawnIoModule Load(string name, byte[] blob, out string why, out bool deviceAbsent) {
            why = null;
            deviceAbsent = false;
            SafeFileHandle h = CreateFile(@"\\?\GLOBALROOT\Device\PawnIO", 0xC0000000u /* GENERIC_READ|WRITE */, 3, IntPtr.Zero, 3 /* OPEN_EXISTING */, 0x80, IntPtr.Zero);
            if (h == null || h.IsInvalid) { why = "cannot open the PawnIO device (" + Win32(Marshal.GetLastWin32Error()) + ")"; deviceAbsent = true; return null; }
            uint ret;
            if (!DeviceIoControl(h, IoctlLoad, blob, (uint)blob.Length, null, 0, out ret, IntPtr.Zero)) {
                why = "the driver rejected the " + name + " module (" + Win32(Marshal.GetLastWin32Error()) + ")";
                h.Close();
                return null;
            }
            return new PawnIoModule(h, name);
        }

        public int Execute(string fn, ulong[] input, ulong[] output, out int returnedCells) {
            returnedCells = 0;
            int nin = input == null ? 0 : input.Length, nout = output == null ? 0 : output.Length;
            var inBuf = new byte[NameLength + nin * 8];
            Encoding.ASCII.GetBytes(fn, 0, Math.Min(fn.Length, NameLength - 1), inBuf, 0);
            for (int i = 0; i < nin; i++) Array.Copy(BitConverter.GetBytes(input[i]), 0, inBuf, NameLength + i * 8, 8);
            var outBuf = new byte[nout * 8];
            uint ret;
            bool ok;
            lock (sync) ok = DeviceIoControl(handle, IoctlExecute, inBuf, (uint)inBuf.Length, outBuf, (uint)outBuf.Length, out ret, IntPtr.Zero);
            if (!ok) return Marshal.GetLastWin32Error();
            returnedCells = (int)(ret / 8);
            for (int i = 0; i < returnedCells && i < nout; i++) output[i] = BitConverter.ToUInt64(outBuf, i * 8);
            return 0;
        }


        public bool Call(string fn, ulong[] input, ulong[] output) { int n; return Execute(fn, input, output, out n) == 0; }

        public void Dispose() { try { handle.Close(); } catch { } }

        internal static string Win32(int code) { return new System.ComponentModel.Win32Exception(code).Message + " [" + code + "]"; }
    }


    public enum DriverInstallResult { Installed, RestartNeeded, Failed }


    public static class PawnIo {
        public const string SetupRepo = "namazso/PawnIO.Setup";
        public const string SetupAsset = "PawnIO_setup.exe";
        public const string HomeUrl = "https://pawnio.eu";

        public const string SourceUrl = "https://github.com/namazso/PawnIO";


        public const string Signer = "namazso.eu";


        public static readonly Version MinVersion = new Version(2, 2, 0);
        const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";

        public static Version InstalledVersion() {
            string v = Reg("DisplayVersion");
            Version ver;
            return v != null && Version.TryParse(v, out ver) ? ver : null;
        }
        public static string InstallLocation() { return Reg("InstallLocation"); }

        public static string InstallFolder() {
            string dir = InstallLocation();
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
            string def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PawnIO");
            return Directory.Exists(def) ? def : dir;
        }
        public static bool Installed { get { return InstalledVersion() != null; } }
        public static bool Outdated { get { Version v = InstalledVersion(); return v != null && v < MinVersion; } }
        static string Reg(string value) {
            try {
                using (RegistryKey k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(UninstallKey))
                    if (k != null) { string s = k.GetValue(value) as string; if (!string.IsNullOrEmpty(s)) return s.Trim(); }
            } catch { }
            return null;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr OpenSCManager(string machine, string db, uint access);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr OpenService(IntPtr scm, string name, uint access);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool QueryServiceStatus(IntPtr svc, out ServiceStatus status);
        [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr h);
        [StructLayout(LayoutKind.Sequential)] struct ServiceStatus { public uint Type, State, Accepted, ExitCode, SpecificExitCode, CheckPoint, WaitHint; }

        public static string ServiceState() {
            IntPtr scm = IntPtr.Zero, svc = IntPtr.Zero;
            try {
                scm = OpenSCManager(null, null, 0x0001 /* SC_MANAGER_CONNECT */);
                if (scm == IntPtr.Zero) return "service manager unavailable";
                svc = OpenService(scm, "PawnIO", 0x0004 /* SERVICE_QUERY_STATUS */);
                if (svc == IntPtr.Zero) return Marshal.GetLastWin32Error() == 1060 ? "not registered" : "cannot query (" + PawnIoModule.Win32(Marshal.GetLastWin32Error()) + ")";
                ServiceStatus st;
                if (!QueryServiceStatus(svc, out st)) return "cannot query";
                switch (st.State) {
                    case 4: return "running";
                    case 1: return "stopped";
                    case 2: case 3: return "starting";
                    default: return "state " + st.State;
                }
            } catch (Exception ex) { return "cannot query (" + ex.Message + ")"; }
            finally { if (svc != IntPtr.Zero) CloseServiceHandle(svc); if (scm != IntPtr.Zero) CloseServiceHandle(scm); }
        }

        public static byte[] Module(string name) {
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Seal.pawnio." + name + ".bin")) {
                if (st == null) throw new InvalidOperationException("embedded module " + name + " missing");
                var ms = new MemoryStream();
                st.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static PawnIoModule Open(string module, out string why) { bool absent; return Open(module, out why, out absent); }


        public static PawnIoModule Open(string module, out string why, out bool deviceAbsent) {
            why = null;
            deviceAbsent = false;
            if (!Installed) { why = "not installed"; deviceAbsent = true; return null; }
            try { return PawnIoModule.Load(module, Module(module), out why, out deviceAbsent); }
            catch (Exception ex) { why = ex.Message; return null; }
        }

        static string NewSetupPath() {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Program.FileStem + "." + Guid.NewGuid().ToString("N") + ".setup.exe");
        }

        public static DriverInstallResult Install(Action<string> progress, out string error) {
            error = null;
            string setup = NewSetupPath();
            try {
                Say(progress, "Finding the installer…");
                Release r = Update.LatestOf(SetupRepo, SetupAsset);
                if (r == null || string.IsNullOrEmpty(r.AssetUrl)) throw new Exception("could not find " + SetupAsset + " on " + SetupRepo);
                Say(progress, "Downloading " + (r.Size > 0 ? (r.Size / 1024 / 1024.0).ToString("0.0") + " MB" : "the installer") + "…");
                Update.Download(r.AssetUrl, setup, r.Size);

                using (new FileStream(setup, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    Say(progress, "Checking the signature…");
                    string signer;
                    if (!Signed(setup, out signer)) throw new Exception("the installer's signature is not " + Signer + (signer != null ? " (it is " + signer + ")" : ""));


                    Version have = InstalledVersion();
                    if (have != null && have < new Version(2, 1, 0)) {
                        Say(progress, "Removing PawnIO " + have + "…");
                        Run(setup, "-uninstall -silent");
                    }
                    Say(progress, "Installing…");
                    int rc = Run(setup, "-install -silent");
                    Log.Write("PawnIO installer exit code " + rc);
                    switch (rc) {
                        case 0: case 183: return Installed ? DriverInstallResult.Installed : Fail("the installer returned " + rc + " but left no installation behind", out error);
                        case 3010: case 1072: return DriverInstallResult.RestartNeeded;
                        default: return Fail("the installer returned " + PawnIoModule.Win32(rc), out error);
                    }
                }
            } catch (Exception ex) { return Fail(ex.Message, out error); }
            finally { try { if (File.Exists(setup)) File.Delete(setup); } catch { } }
        }
        static DriverInstallResult Fail(string why, out string error) { error = why; Log.Write("PawnIO install failed: " + why); return DriverInstallResult.Failed; }
        static void Say(Action<string> progress, string s) { Log.Write("PawnIO: " + s); if (progress != null) { try { progress(s); } catch { } } }

        public static bool Uninstall(out string error) {
            error = null;
            string copy = null;
            try {
                string dir = InstallFolder();
                string exe = null;
                if (!string.IsNullOrEmpty(dir)) {
                    foreach (string cand in new string[] { Path.Combine(dir, "uninstall.exe"), Path.Combine(dir, SetupAsset) })
                        if (File.Exists(cand)) { exe = cand; break; }
                }
                if (exe == null) {
                    if (!Installed) return true;
                    throw new Exception("no uninstaller in " + (dir ?? "(unknown folder)"));
                }

                copy = Path.Combine(Path.GetTempPath(), Program.AppName + "-pawnio-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(copy);
                string runner = Path.Combine(copy, "uninstall.exe");
                File.Copy(exe, runner);
                int rc = Run(runner, "-uninstall -silent");
                Log.Write("PawnIO uninstaller exit code " + rc + " · service " + ServiceState());


                if (!Installed) return true;
                throw new Exception(rc == 0 ? "the driver is still registered" : "the uninstaller returned " + PawnIoModule.Win32(rc));
            } catch (Exception ex) { error = ex.Message; Log.Write("PawnIO uninstall failed: " + ex.Message); return false; }
            finally { try { if (copy != null) Directory.Delete(copy, true); } catch { } }
        }

        static int Run(string exe, string args) {


            using (Process p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() })) {
                if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } throw new Exception("the installer did not finish in three minutes"); }
                return p.ExitCode;
            }
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WintrustFileInfo { public uint cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }
        [StructLayout(LayoutKind.Sequential)]
        struct WintrustData {
            public uint cbStruct; public IntPtr pPolicyCallbackData, pSIPClientData; public uint dwUIChoice, fdwRevocationChecks, dwUnionChoice;
            public IntPtr pFile; public uint dwStateAction; public IntPtr hWVTStateData, pwszURLReference; public uint dwProvFlags, dwUIContext; public IntPtr pSignatureSettings;
        }
        [DllImport("wintrust.dll", ExactSpelling = true)] static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, IntPtr data);
        static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        public static bool Signed(string path, out string signer) {
            signer = null;
            IntPtr pInfo = IntPtr.Zero, pData = IntPtr.Zero;
            try {
                var fi = new WintrustFileInfo { cbStruct = (uint)Marshal.SizeOf(typeof(WintrustFileInfo)), pcwszFilePath = path };
                pInfo = Marshal.AllocHGlobal((int)fi.cbStruct);
                Marshal.StructureToPtr(fi, pInfo, false);
                var wd = new WintrustData {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WintrustData)), dwUIChoice = 2 /* WTD_UI_NONE */, fdwRevocationChecks = 0 /* WTD_REVOKE_NONE */,
                    dwUnionChoice = 1 /* WTD_CHOICE_FILE */, pFile = pInfo, dwStateAction = 0 /* WTD_STATEACTION_IGNORE */,
                    dwProvFlags = 0x10 | 0x1000 /* WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL */
                };
                pData = Marshal.AllocHGlobal((int)wd.cbStruct);
                Marshal.StructureToPtr(wd, pData, false);
                int hr = WinVerifyTrust(new IntPtr(-1), GenericVerifyV2, pData);
                if (hr != 0) { Log.Write("PawnIO installer signature: WinVerifyTrust 0x" + hr.ToString("X8")); return false; }
                var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                signer = cert.GetNameInfo(X509NameType.SimpleName, false);
                return signer != null && signer.Equals(Signer, StringComparison.OrdinalIgnoreCase);
            } catch (Exception ex) { Log.Write("PawnIO installer signature: " + ex.Message); return false; }
            finally { if (pData != IntPtr.Zero) Marshal.FreeHGlobal(pData); if (pInfo != IntPtr.Zero) Marshal.FreeHGlobal(pInfo); }
        }
    }
}
