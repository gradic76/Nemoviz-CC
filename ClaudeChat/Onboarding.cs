using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// What other users need (author, 2026-11-07, before distribution): projects wherever they are,
/// signing in to Claude Code from the window, and installing Claude Code when it is missing.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmLogin = 208, IdmInstallClaude = 209, IdmOpenExisting = 213;

    // ------------------------------------------------------------------ projects anywhere

    private sealed record Discovered(DateTime Stamp, string? Folder);
    private static readonly Dictionary<string, Discovered> s_discovered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every folder Claude Code has worked in (desktop app included): ~\.claude\projects keeps one
    /// subfolder per working folder, and its transcripts name the folder in "cwd". Folders that no
    /// longer exist, the home folder and drive roots are left out.
    /// </summary>
    private static IEnumerable<string> DiscoveredProjects()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        DirectoryInfo[] dirs;
        try { dirs = new DirectoryInfo(root).GetDirectories(); } catch { yield break; }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
        foreach (var d in dirs)
        {
            if (!s_discovered.TryGetValue(d.FullName, out var c) || c.Stamp != d.LastWriteTimeUtc)
                s_discovered[d.FullName] = c = new Discovered(d.LastWriteTimeUtc, ReadCwd(d));
            string? folder = c.Folder;
            if (folder == null || !Directory.Exists(folder)) continue;
            folder = folder.TrimEnd('\\');
            if (folder.Equals(home, StringComparison.OrdinalIgnoreCase) || folder.EndsWith(':')) continue;
            if (folder.StartsWith(Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
            yield return folder;
        }
    }

    private static string? ReadCwd(DirectoryInfo dir)
    {
        try
        {
            var file = dir.GetFiles("*.jsonl").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (file == null) return null;
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            for (int i = 0; i < 200 && reader.ReadLine() is { } line; i++)
            {
                if (!line.Contains("\"cwd\"")) continue;
                if (JsonNode.Parse(line)?["cwd"]?.GetValue<string>() is { Length: > 0 } cwd) return cwd;
            }
        }
        catch { }
        return null;
    }

    private bool UnderRoot(string folder) =>
        SameFolder(Path.GetDirectoryName(folder.TrimEnd('\\')) ?? "", ProjectsRoot);

    /// <summary>The Code list: subfolders of the projects folder, folders Claude Code has worked
    /// in, and folders opened by hand — without Chat and without the ones taken out of the list.</summary>
    private List<string> ProjectFolders()
    {
        var all = new List<string>();
        try { all.AddRange(Directory.GetDirectories(ProjectsRoot).Where(d => !Path.GetFileName(d).StartsWith('.'))); } catch { }
        all.AddRange(DiscoveredProjects());
        all.AddRange(_state.ExtraProjects.Where(Directory.Exists));
        return all.Select(p => Path.GetFullPath(p).TrimEnd('\\'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => !SameFolder(p, ChatFolderPath) && !IsHidden(p))
            .OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>A project's name in the lists; outside the projects folder with where it is.</summary>
    private string ProjectLabel(string folder)
    {
        string name = _state.ProjectTitles.TryGetValue(Path.GetFullPath(folder).TrimEnd('\\'), out var t) ? t : Path.GetFileName(folder);
        return UnderRoot(folder) ? name : $"{name} ({Path.GetDirectoryName(folder)})";
    }

    // ------------------------------------------------------------------ sign in / install

    private void StartLogin()
    {
        _claudeExe ??= ClaudeLocator.Find(_settings.ClaudePath);
        if (_claudeExe == null) { OfferClaudeInstall(); return; }
        Win32.MessageBoxW(_hwnd, Crlf(_loc["login.info"]), _settings.AppName, Win32.MB_OK | Win32.MB_ICONINFORMATION);
        try
        {
            Process.Start(new ProcessStartInfo(_claudeExe)
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            });
        }
        catch (Exception e) { AddEntry(_loc["role.system"], e.Message); }
        // A sign-in changes nothing in a running Claude Code: the next message starts a fresh one.
        if (!_busy) { _session?.Kill(); _session = null; }
    }

    private void OfferClaudeInstall()
    {
        int r = Win32.MessageBoxW(_hwnd, Crlf(_loc["install.ask"]), _settings.AppName, 0x04 /* YESNO */ | 0x20 /* QUESTION */);
        if (r != 6) return;
        try
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = true };
            psi.Arguments = Compat.JoinArguments(new[] { "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "irm https://claude.ai/install.ps1 | iex" });
            Process.Start(psi);
        }
        catch (Exception e) { AddEntry(_loc["role.system"], e.Message); }
    }

    /// <summary>Claude Code's own words for "not signed in" (expired, missing, refused).</summary>
    private static bool LooksLikeSignInProblem(string text) =>
        text.Contains("/login", StringComparison.OrdinalIgnoreCase)
        || text.Contains("OAuth", StringComparison.OrdinalIgnoreCase)
        || text.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Invalid API key", StringComparison.OrdinalIgnoreCase)
        || text.Contains("authentication", StringComparison.OrdinalIgnoreCase);
}
