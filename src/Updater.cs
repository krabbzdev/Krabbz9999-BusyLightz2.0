using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Krabbz.Blynclight
{
    // A newer release found on GitHub, downloaded and checked against GitHub's SHA-256 digest.
    public class UpdateInfo
    {
        public Version Version;
        public string MsiPath;
    }

    // Checks the public GitHub repo's latest release and installs it if it's newer than this build.
    public static class Updater
    {
        static readonly string updateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krabbz9999 Blynclight", "updates");

        // Returns the downloaded update, or null when this build is current. Throws on network or verification errors.
        public static UpdateInfo DownloadIfNewer(string repo, Version current)
        {
            // .NET 4.x doesn't enable TLS 1.2 by default; GitHub requires it.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var release = (Dictionary<string, object>)new JavaScriptSerializer()
                .DeserializeObject(Get("https://api.github.com/repos/" + repo + "/releases/latest"));
            Version latest;
            if (!Version.TryParse(((string)release["tag_name"]).TrimStart('v', 'V'), out latest)) return null;
            if (latest <= new Version(current.Major, current.Minor, current.Build)) return null;

            Dictionary<string, object> msi = null;
            foreach (Dictionary<string, object> asset in (IEnumerable)release["assets"])
                if (((string)asset["name"]).EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) msi = asset;
            if (msi == null) throw new InvalidOperationException("Release " + latest + " has no .msi file.");

            // Only install files whose hash matches what GitHub recorded at upload time.
            object digest;
            string expected = msi.TryGetValue("digest", out digest) ? digest as string : null;
            if (expected == null || !expected.StartsWith("sha256:"))
                throw new InvalidOperationException("Release " + latest + " has no SHA-256 digest; not installing it.");
            expected = expected.Substring("sha256:".Length);

            Directory.CreateDirectory(updateDir);
            foreach (var old in Directory.GetFiles(updateDir, "*.msi")) File.Delete(old);
            string path = Path.Combine(updateDir, "KrabbzDev-BusyLightz2.0-" + latest + ".msi");
            using (var web = NewClient()) web.DownloadFile((string)msi["browser_download_url"], path);

            if (!string.Equals(Sha256(path), expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new InvalidOperationException("Downloaded update failed its SHA-256 check; deleted it.");
            }
            return new UpdateInfo { Version = latest, MsiPath = path };
        }

        // Installs the per-user MSI silently from a hidden helper, then reopens the app. The caller must exit right away:
        // the helper waits a couple of seconds so the app's file isn't in use when the installer replaces it.
        public static void Install(UpdateInfo update, string appPath, bool showWindow)
        {
            string log = Path.Combine(updateDir, "install-" + update.Version + ".log");
            string relaunchArgs = (showWindow ? "" : "--tray ") + "--updated";
            string script =
                "ping -n 3 127.0.0.1 >nul" +
                " & start \"\" /wait msiexec.exe /i \"" + update.MsiPath + "\" /qn /l*v \"" + log + "\"" +
                " & start \"\" \"" + appPath + "\" " + relaunchArgs;
            var helper = new ProcessStartInfo("cmd.exe", "/c " + script);
            helper.UseShellExecute = false;
            helper.CreateNoWindow = true;
            helper.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(helper);
        }

        static string Get(string url)
        {
            using (var web = NewClient())
            {
                web.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                return web.DownloadString(url);
            }
        }

        static WebClient NewClient()
        {
            var web = new WebClient();
            web.Headers[HttpRequestHeader.UserAgent] = "KrabbzDev-BusyLightz2.0";
            return web;
        }

        static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
    }
}
