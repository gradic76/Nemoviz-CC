using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// Updates and help (author, 2026-11-07/08). Help → Check for updates, and on its own at start at
/// most once a day (Settings → General): GitHub's latest release is compared with this build. A
/// newer one is offered in a box with its What's new (the release text; "## Hrvatski" and
/// "## English" sections pick the language). Update downloads it, checks size and SHA-256, waits
/// for the current answer to finish, and installs without further questions:
/// - installed: the Inno setup runs with /VERYSILENT and /relaunch=1, and opens the program again;
/// - portable (settings.json beside the exe): the zip is unpacked by the program itself. A running
///   exe cannot be overwritten but can be renamed, so each old file becomes *.ncc-old (removed at the
///   next start), the new one takes its place, and settings.json is left alone. No SFX is needed.
/// Help → Instructions (F1) and What's new open HTML pages in the browser.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmCheckUpdates = 402, IdmHelp = 403, IdmWhatsNew = 404;
    private const string Repo = "gradic76/Nemoviz-CC";
    private const string SetupAsset = "NemovizCC-Setup.exe", ZipAsset = "NemovizCC-portable.zip";
    private bool _updateBusy;
    private string? _pendingInstall;
    private const string OldSuffix = ".ncc-old"; // distinctive: only these are removed at start

    private static Version CurrentVersion
    {
        get
        {
            // For testing the offer against a real release.
            if (Version.TryParse(Environment.GetEnvironmentVariable("NEMOVIZ_FAKE_VERSION"), out var fake)) return fake;
            return typeof(MainWindow).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);
        }
    }

    private static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, "settings.json"));

    /// <summary>Started by pokreni.cmd: a development copy, which an update must not replace.</summary>
    private bool IsDevelopmentCopy => !string.IsNullOrEmpty(_settings.RestartCommand);

    private static HttpClient NewHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NemovizCC/" + CurrentVersion);
        return http;
    }

    /// <summary>Once a day at start, if allowed; silent unless there is something new.</summary>
    private void AutoCheckForUpdates()
    {
        if (!_settings.CheckUpdates || IsDevelopmentCopy) return;
        if (_state.LastUpdateCheck is { } last && DateTime.UtcNow - last < TimeSpan.FromHours(20)) return;
        CheckForUpdates(manual: false);
    }

    private sealed record ReleaseInfo(Version Latest, string Notes, string Asset, string Url, long Size, string? Sha256);

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
                var json = JsonNode.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
                Version.TryParse((json?["tag_name"]?.GetValue<string>() ?? "").TrimStart('v', 'V'), out var latest);
                string want = IsPortable ? ZipAsset : SetupAsset;
                var asset = (json?["assets"] as JsonArray)?.FirstOrDefault(a => a?["name"]?.GetValue<string>() == want);
                string? url = asset?["browser_download_url"]?.GetValue<string>();
                string? digest = asset?["digest"]?.GetValue<string>();
                string? sha = digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : null;
                var info = latest == null || url == null ? null
                    : new ReleaseInfo(latest, json?["body"]?.GetValue<string>() ?? "", want, url, asset?["size"]?.GetValue<long>() ?? 0, sha);
                Post(() => OnUpdateInfo(manual, info));
            }
            catch (Exception e) { Post(() => { _updateBusy = false; if (manual) Notice(_hwnd, _loc.Format("upd.failed", e.Message)); }); }
        });
    }

    /// <summary>The part of the release text in the window's language, without markdown marks.</summary>
    private string ReleaseNotesText(string body)
    {
        string wanted = _loc.Code.StartsWith("hr", StringComparison.OrdinalIgnoreCase) ? "## Hrvatski" : "## English";
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        int start = lines.FindIndex(l => l.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (start >= 0)
        {
            int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith("## "));
            lines = lines.GetRange(start + 1, (end < 0 ? lines.Count : end) - start - 1);
        }
        string text = string.Join("\n", lines).Replace("**", "").Trim();
        return text.Length > 0 ? text : _loc["upd.noNotes"];
    }

    private void OnUpdateInfo(bool manual, ReleaseInfo? r)
    {
        // Not over a dialog that is open: the next start offers it again.
        if (_dlg != IntPtr.Zero || _form != null) { _updateBusy = false; return; }
        _state.LastUpdateCheck = DateTime.UtcNow;
        _state.Save();
        if (r == null || r.Latest <= CurrentVersion)
        {
            _updateBusy = false;
            if (manual) Notice(_hwnd, _loc.Format("upd.none", CurrentVersion));
            return;
        }
        if (!manual && _state.SkippedVersion == r.Latest.ToString()) { _updateBusy = false; return; }

        if (!ShowUpdateOffer(r))
        {
            _state.SkippedVersion = r.Latest.ToString(); // no more reminders at start for this one
            _state.Save();
            _updateBusy = false;
            return;
        }
        if (IsDevelopmentCopy) { _updateBusy = false; Notice(_hwnd, _loc["upd.dev"]); return; }
        Download(r);
    }

    /// <summary>The offer: what is new, Update, Not now. True when the user chose Update.</summary>
    private bool ShowUpdateOffer(ReleaseInfo r)
    {
        var f = BeginForm(_loc["upd.title"]);
        f.BigLabel = FormLabel(f, _loc.Format("upd.whatsnew", r.Latest, CurrentVersion));
        f.Big = FormChild(f, "EDIT", ToEdit(ReleaseNotesText(r.Notes)),
            Win32.ES_MULTILINE | Win32.ES_AUTOVSCROLL | Win32.WS_VSCROLL | Win32.WS_TABSTOP | Win32.ES_READONLY, Win32.WS_EX_CLIENTEDGE);
        FButton(f, FormOk, _loc["upd.install"], true);
        FButton(f, FormCancel, _loc["upd.later"]);
        f.CustomLayout = () =>
        {
            int pad = S(10), gap = S(6), lineH = Measure("Ag").cy, rowH = lineH + S(12), labelH = lineH + S(4);
            int clientW = S(560), clientH = SizeForm(f, clientW, S(400));
            int by = clientH - pad - rowH, boxY = pad + labelH;
            Move(f.BigLabel, pad, pad, clientW - 2 * pad, labelH);
            Move(f.Big, pad, boxY, clientW - 2 * pad, by - gap - boxY);
            int x = clientW - pad;
            for (int i = f.Buttons.Count - 1; i >= 0; i--)
            {
                int w = Math.Max(S(90), Measure(Win32.GetText(f.Buttons[i])).cx + S(24));
                x -= w;
                Move(f.Buttons[i], x, by, w, rowH);
                x -= gap;
            }
        };
        return RunForm(f, f.Big) == FormOk;
    }

    private void Download(ReleaseInfo r)
    {
        string target = Path.Combine(Path.GetTempPath(), r.Asset);
        SetLive(_loc["upd.downloading"]);
        _speech.Speak(_loc["upd.downloading"], true);
        Task.Run(async () =>
        {
            try
            {
                using var http = NewHttp();
                using (var src = await http.GetStreamAsync(r.Url))
                using (var dst = File.Create(target + ".part"))
                    await src.CopyToAsync(dst);

                // The file is about to be run: it has to be exactly what was published.
                var info = new FileInfo(target + ".part");
                bool good = r.Size <= 0 || info.Length == r.Size;
                if (good && r.Sha256 != null)
                {
                    using var read = File.OpenRead(target + ".part");
                    good = Convert.ToHexString(await SHA256.HashDataAsync(read)).Equals(r.Sha256, StringComparison.OrdinalIgnoreCase);
                }
                if (!good) throw new InvalidDataException(_loc["upd.badFile"]);
                File.Move(target + ".part", target, overwrite: true);
                Post(() => { SetLive(_loc["live.idle"]); _pendingInstall = target; InstallWhenIdle(announce: true); });
            }
            catch (Exception e)
            {
                try { File.Delete(target + ".part"); } catch { }
                Post(() => { _updateBusy = false; SetLive(_loc["live.idle"]); Notice(_hwnd, _loc.Format("upd.failed", e.Message)); });
            }
        });
    }

    /// <summary>Installs the downloaded update as soon as no answer is in progress and no dialog is open.</summary>
    private void InstallWhenIdle(bool announce = false)
    {
        if (_pendingInstall == null) return;
        if (_busy || _stopping || _dlg != IntPtr.Zero || _form != null)
        {
            if (announce) _speech.Speak(_loc["upd.waiting"], true);
            return;
        }
        string path = _pendingInstall;
        _pendingInstall = null;
        _speech.Speak(_loc["upd.installing"], true);
        try
        {
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) RunSilentSetup(path);
            else ReplacePortableFiles(path);
            Win32.PostMessageW(_hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception e)
        {
            _updateBusy = false;
            Notice(_hwnd, _loc.Format("upd.failed", e.Message));
        }
    }

    private static void RunSilentSetup(string setup)
    {
        // The same kind of installation as now: for everyone (Windows asks for permission) or just for this user.
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        bool forAll = AppContext.BaseDirectory.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase);
        var psi = new ProcessStartInfo(setup) { UseShellExecute = true };
        foreach (var a in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/relaunch=1", forAll ? "/ALLUSERS" : "/CURRENTUSER" })
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    /// <summary>Puts the zip's files in place of this program's and starts the new one once this
    /// process has gone. Anything that fails on the way is put back.</summary>
    private static void ReplacePortableFiles(string zip)
    {
        string root = AppContext.BaseDirectory;
        string rootFull = Path.GetFullPath(root);
        var unpacked = new List<string>();
        var replaced = new List<string>();
        try
        {
            using (var archive = ZipFile.OpenRead(zip))
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    int slash = name.IndexOf('/');
                    string relative = slash >= 0 ? name[(slash + 1)..] : name; // the zip has one top folder
                    if (relative.Length == 0 || relative.EndsWith('/')) continue;
                    if (relative.Equals("settings.json", StringComparison.OrdinalIgnoreCase)) continue; // the user's
                    string dest = Path.GetFullPath(Path.Combine(root, relative));
                    if (!dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest + ".new", overwrite: true);
                    unpacked.Add(dest);
                }
            if (unpacked.Count == 0) throw new InvalidDataException("empty archive");

            foreach (var dest in unpacked)
            {
                if (File.Exists(dest))
                {
                    File.Move(dest, dest + OldSuffix, overwrite: true);
                    replaced.Add(dest);
                }
                File.Move(dest + ".new", dest);
            }
        }
        catch
        {
            foreach (var dest in replaced)
                try { File.Delete(dest); File.Move(dest + OldSuffix, dest); } catch { }
            foreach (var dest in unpacked)
                try { File.Delete(dest + ".new"); } catch { }
            throw;
        }
        var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WorkingDirectory = root };
        psi.ArgumentList.Add($"--wait-pid={Environment.ProcessId}");
        Process.Start(psi);
    }

    /// <summary>At start: the *.ncc-old files a portable update left behind.</summary>
    private static void RemoveOldFiles()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(AppContext.BaseDirectory, "*" + OldSuffix, SearchOption.AllDirectories))
                try { File.Delete(f); } catch { }
        }
        catch { }
    }

    // ------------------------------------------------------------------ help

    private void ShowHelp() => ShowHtml("Pomoc.html", "Help.html");
    private void ShowWhatsNew() => ShowHtml("Novosti.html", "WhatsNew.html");

    private void ShowHtml(string croatian, string english)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "help");
        string file = Path.Combine(dir, _loc.Code.StartsWith("hr", StringComparison.OrdinalIgnoreCase) ? croatian : english);
        if (!File.Exists(file)) file = Path.Combine(dir, english);
        try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); }
        catch (Exception e) { Notice(_hwnd, e.Message); }
    }
}
