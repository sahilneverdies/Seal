

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Seal {


    public sealed class Release {
        public string Tag;
        public string AssetUrl;
        public long Size;
    }

    public static class Update {
        public const string Repo = "sahilneverdies/Seal";
        public static string ReleasesUrl { get { return "https://github.com/" + Repo + "/releases/latest"; } }
        const string AssetName = "Seal.exe";


        static string Dir { get { return AppDomain.CurrentDomain.BaseDirectory; } }


        public static string ExePath {
            get {
                try { string p = Assembly.GetEntryAssembly().Location; if (!string.IsNullOrEmpty(p)) return p; } catch { }
                try { return Process.GetCurrentProcess().MainModule.FileName; } catch { return null; }
            }
        }

        public static string StagePath { get { return Path.Combine(Dir, Program.FileStem + ".update.exe"); } }

        public static string OldPath { get { return Path.Combine(Dir, Program.FileStem + ".old.exe"); } }

        public static bool CanReplace {
            get {
                try {
                    if (Directory.Exists(Path.Combine(Dir, "src"))) return false;
                    string exe = ExePath;
                    return !string.IsNullOrEmpty(exe) && File.Exists(exe);
                } catch { return false; }
            }
        }

        public static Release Latest() { return LatestOf(Repo, AssetName); }

        public static Release LatestOf(string repo, string assetName) {
            try {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + repo + "/releases/latest");
                req.UserAgent = Program.AppName + "/" + Program.Version;
                req.Accept = "application/vnd.github+json";
                req.Timeout = req.ReadWriteTimeout = 8000;
                string body;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream())) body = sr.ReadToEnd();
                string tag = Field(body, "tag_name", 0);
                if (tag == null) return null;
                var r = new Release { Tag = tag.TrimStart('v', 'V') };

                int at = body.IndexOf("\"name\":\"" + assetName + "\"", StringComparison.OrdinalIgnoreCase);
                if (at >= 0) {
                    r.AssetUrl = Field(body, "browser_download_url", at);
                    long n;
                    if (long.TryParse(Number(body, "size", at) ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) r.Size = n;
                }
                Log.Write(repo + ": latest release " + r.Tag + (repo == Repo ? ", running " + Program.Version : "")
                    + (r.AssetUrl == null ? " (no " + assetName + " asset on it)" : ""));
                return r;
            } catch (Exception ex) { Log.Write(repo + " release check failed: " + ex.Message); return null; }
        }


        public static string LatestTag() { var r = Latest(); return r == null ? null : r.Tag; }


        static string Field(string json, string name, int from) {
            int i = json.IndexOf("\"" + name + "\"", from, StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i);
            if (i < 0) return null;
            i = json.IndexOf('"', i);
            if (i < 0) return null;
            int end = json.IndexOf('"', i + 1);
            if (end < 0) return null;
            return json.Substring(i + 1, end - i - 1);
        }

        static string Number(string json, string name, int from) {
            int i = json.IndexOf("\"" + name + "\"", from, StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i);
            if (i < 0) return null;
            int j = i + 1;
            while (j < json.Length && (json[j] == ' ' || json[j] == '\t' || json[j] == '\r' || json[j] == '\n')) j++;
            int s = j;
            while (j < json.Length && char.IsDigit(json[j])) j++;
            return j > s ? json.Substring(s, j - s) : null;
        }

        public static bool Stage(Release r) {
            if (r == null || string.IsNullOrEmpty(r.AssetUrl) || !CanReplace) return false;
            if (!Newer(r.Tag, Program.Version)) return false;
            string part = StagePath + ".part";
            try {
                Download(r.AssetUrl, part, r.Size);
                if (!Verify(part, r)) throw new Exception("what arrived is not " + AssetName + " " + r.Tag);
                try { if (File.Exists(StagePath)) File.Delete(StagePath); } catch { }
                File.Move(part, StagePath);
                Log.Write("update " + r.Tag + " staged");
                return true;
            } catch (Exception ex) {
                Log.Write("update download failed: " + ex.Message);
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                return false;
            }
        }

        public static void Download(string url, string dest, long expectedSize) {
            try {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = Program.AppName + "/" + Program.Version;
                req.Timeout = 15000;
                req.ReadWriteTimeout = 60000;
                using (var resp = (HttpWebResponse)req.GetResponse()) {


                    Uri u = resp.ResponseUri;
                    string host = u.Host;
                    bool ok = u.Scheme == "https" && (host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                        || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
                        || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
                    if (!ok) throw new Exception("redirected to " + u.Scheme + "://" + host);
                    using (var src = resp.GetResponseStream())
                    using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None)) {
                        var buf = new byte[64 * 1024];
                        int n;
                        while ((n = src.Read(buf, 0, buf.Length)) > 0) dst.Write(buf, 0, n);
                    }
                }
                long len = new FileInfo(dest).Length;
                if (expectedSize > 0 && len != expectedSize) throw new Exception(len + " bytes arrived, the release says " + expectedSize);
            } catch {
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                throw;
            }
        }

        static bool Verify(string path, Release r) {
            var fi = new FileInfo(path);
            if (r.Size > 0 && fi.Length != r.Size) { Log.Write("update: " + fi.Length + " bytes, the release says " + r.Size); return false; }
            string v = VersionOf(path);
            if (string.IsNullOrEmpty(v)) { Log.Write("update: the download has no version resource"); return false; }
            if (v != r.Tag && !v.StartsWith(r.Tag + ".", StringComparison.Ordinal)) {
                Log.Write("update: the download says " + v + ", the release says " + r.Tag);
                return false;
            }
            return true;
        }
        static string VersionOf(string path) {
            try { string v = FileVersionInfo.GetVersionInfo(path).ProductVersion; return v == null ? null : v.Trim(); } catch { return null; }
        }


        public static string Staged() {
            try {
                if (!CanReplace || !File.Exists(StagePath)) return null;
                string v = VersionOf(StagePath);
                return Newer(v, Program.Version) ? v : null;
            } catch { return null; }
        }

        public static bool Swap(out string error) {
            error = null;
            string exe = ExePath, stage = StagePath, old = OldPath;
            bool renamed = false, moved = false;
            try {
                if (string.IsNullOrEmpty(exe)) throw new Exception("cannot find the running executable");
                if (!File.Exists(stage)) throw new Exception("nothing staged");
                try { if (File.Exists(old)) File.Delete(old); } catch { }

                File.Move(exe, old); renamed = true;
                File.Move(stage, exe); moved = true;

                Process child = Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = false, WorkingDirectory = Dir });

                if (child != null && child.WaitForExit(2000))
                    throw new Exception("the new build exited immediately (code " + child.ExitCode + ")");
                Log.Write("update: replaced " + Path.GetFileName(exe) + ", started the new build");
                return true;
            } catch (Exception ex) {


                error = ex.Message;
                Log.Write("update swap failed: " + ex.Message);
                try { if (moved) File.Move(exe, stage); } catch { }
                try { if (renamed) File.Move(old, exe); } catch { }
                return false;
            }
        }

        public static void CleanOld() {
            try {
                string old = OldPath;
                for (int i = 0; i < 20 && File.Exists(old); i++) {
                    try { File.Delete(old); } catch { Thread.Sleep(250); }
                }
                if (File.Exists(old)) Log.Write("update: " + Path.GetFileName(old) + " is still in use, leaving it");
                else Log.Write("update: removed the build we replaced");
            } catch { }
        }

        public static bool Newer(string a, string b) {
            if (string.IsNullOrEmpty(a)) return false;
            string[] pa = a.TrimStart('v', 'V').Split('.'), pb = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++) {
                int na = Part(pa, i), nb = Part(pb, i);
                if (na != nb) return na > nb;
            }
            return false;
        }
        static int Part(string[] parts, int i) {
            int n;
            return i < parts.Length && int.TryParse(new string(Array.FindAll(parts[i].ToCharArray(), char.IsDigit)), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }


        public static string Ago(DateTime when) {
            if (when == DateTime.MinValue) return "never checked";
            var d = DateTime.Now - when;
            if (d.TotalMinutes < 2) return "checked just now";
            if (d.TotalHours < 1) return "checked " + (int)d.TotalMinutes + " min ago";
            if (d.TotalHours < 24) return "checked " + (int)d.TotalHours + (d.TotalHours < 2 ? " hour ago" : " hours ago");
            int days = (int)d.TotalDays;
            return "checked " + days + (days == 1 ? " day ago" : " days ago");
        }
    }
}
