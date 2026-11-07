using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// Updates and help (author, 2026-11-07). Help → Check for updates, and on its own at start at
/// most once a day (Settings → General): GitHub's latest release is compared with this build;
/// a newer one is offered with Yes/No, and Yes downloads the installer — or, for the portable
/// build (settings.json beside the exe), the zip — into Downloads. The installer can then be run
/// straight away (the window closes); the zip is shown in Explorer.
/// Help → Instructions (F1) opens help\Pomoc.html or help\Help.html in the browser: read-only,
/// headings to jump by.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmCheckUpdates = 402, IdmHelp = 403;
    private const string Repo = "gradic76/Nemoviz-CC";
    private const string SetupAsset = "NemovizCC-Setup.exe", ZipAsset = "NemovizCC-portable.zip";
    private bool _updateBusy;

    private static Version CurrentVersion =>
        typeof(MainWindow).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);

    private static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, "settings.json"));

    private static HttpClient NewHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NemovizCC/" + CurrentVersion);
        return http;
    }

    /// <summary>Once a day at start, if allowed; silent unless there is something new.</summary>
    private void AutoCheckForUpdates()
    {
        if (!_settings.CheckUpdates) return;
        if (_state.LastUpdateCheck is { } last && DateTime.UtcNow - last < TimeSpan.FromHours(20)) return;
        CheckForUpdates(manual: false);
    }

    private void CheckForUpdates(bool manual)
    {
        if (_updateBusy) return;
        _updateBusy = true;
        if (manual) _speech.Speak(_loc["upd.checking"], true);
        Task.Run(async () =>
        {
            try
            {
                using var http = NewHttp();
                string body = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
                var json = JsonNode.Parse(body);
                string tag = json?["tag_name"]?.GetValue<string>() ?? "";
                string want = IsPortable ? ZipAsset : SetupAsset;
                string? url = (json?["assets"] as JsonArray)?
                    .FirstOrDefault(a => a?["name"]?.GetValue<string>() == want)?["browser_download_url"]?.GetValue<string>();
                Post(() => OnUpdateInfo(manual, tag, want, url));
            }
            catch (Exception e) { Post(() => { _updateBusy = false; if (manual) Notice(_hwnd, _loc.Format("upd.failed", e.Message)); }); }
        });
    }

    private void OnUpdateInfo(bool manual, string tag, string asset, string? url)
    {
        _state.LastUpdateCheck = DateTime.UtcNow;
        _state.Save();
        Version.TryParse(tag.TrimStart('v', 'V'), out var latest);
        if (latest == null || latest <= CurrentVersion || url == null)
        {
            _updateBusy = false;
            if (manual) Notice(_hwnd, _loc.Format("upd.none", CurrentVersion));
            return;
        }
        if (!manual && _state.SkippedVersion == latest.ToString()) { _updateBusy = false; return; }

        int r = Win32.MessageBoxW(_hwnd, Crlf(_loc.Format("upd.ask", latest, CurrentVersion, asset)), _settings.AppName,
            0x04 /* YESNO */ | 0x40 /* INFORMATION */);
        if (r != 6)
        {
            _state.SkippedVersion = latest.ToString(); // no more reminders at start for this one
            _state.Save();
            _updateBusy = false;
            return;
        }
        Download(url, asset);
    }

    private void Download(string url, string asset)
    {
        string target = Path.Combine(DownloadsFolder(), asset);
        SetLive(_loc["upd.downloading"]);
        _speech.Speak(_loc["upd.downloading"], true);
        Task.Run(async () =>
        {
            try
            {
                using var http = NewHttp();
                using (var src = await http.GetStreamAsync(url))
                using (var dst = File.Create(target + ".part"))
                    await src.CopyToAsync(dst);
                File.Move(target + ".part", target, overwrite: true);
                Post(() => Downloaded(target));
            }
            catch (Exception e)
            {
                try { File.Delete(target + ".part"); } catch { }
                Post(() => { _updateBusy = false; SetLive(_loc["live.idle"]); Notice(_hwnd, _loc.Format("upd.failed", e.Message)); });
            }
        });
    }

    private void Downloaded(string path)
    {
        _updateBusy = false;
        SetLive(_loc["live.idle"]);
        PlayBell();
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            int r = Win32.MessageBoxW(_hwnd, Crlf(_loc.Format("upd.runSetup", path)), _settings.AppName, 0x04 | 0x20 /* QUESTION */);
            if (r == 6)
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch (Exception e) { Notice(_hwnd, e.Message); return; }
                Win32.PostMessageW(_hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        else
        {
            Notice(_hwnd, _loc.Format("upd.zipReady", path));
            try { Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

    private static string DownloadsFolder()
    {
        try
        {
            if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out IntPtr p) == 0)
            {
                string s = Marshal.PtrToStringUni(p) ?? "";
                Marshal.FreeCoTaskMem(p);
                if (Directory.Exists(s)) return s;
            }
        }
        catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    // ------------------------------------------------------------------ help

    private void ShowHelp()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "help");
        string file = Path.Combine(dir, _loc.Code.StartsWith("hr", StringComparison.OrdinalIgnoreCase) ? "Pomoc.html" : "Help.html");
        if (!File.Exists(file)) file = Path.Combine(dir, "Help.html");
        try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); }
        catch (Exception e) { Notice(_hwnd, e.Message); }
    }
}
