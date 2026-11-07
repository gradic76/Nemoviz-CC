using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClaudeChat;

/// <summary>
/// Finds claude.exe. Order: settings "claudePath" → env CLAUDE_CHAT_CLAUDE → PATH →
/// the copy the Claude desktop app keeps (newest version) → the native installer's ~/.local/bin.
/// </summary>
public static class ClaudeLocator
{
    public static string? Find(string? configured)
    {
        foreach (var candidate in Candidates(configured))
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return Path.GetFullPath(candidate);
        return null;
    }

    private static IEnumerable<string?> Candidates(string? configured)
    {
        yield return Expand(configured);
        yield return Expand(Environment.GetEnvironmentVariable("CLAUDE_CHAT_CLAUDE"));

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string? p = null;
            try { if (dir.Length > 0) p = Path.Combine(dir.Trim('"'), "claude.exe"); } catch { }
            yield return p;
        }

        // %LOCALAPPDATA%\Claude-Data\<profile>\claude-code\<version>\<hash>\claude.exe
        yield return NewestDesktopCopy(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claude-Data"));
        // Older desktop builds: %APPDATA%\Claude\claude-code\<version>\...
        yield return NewestDesktopCopy(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude"));

        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
    }

    private static string? Expand(string? p) => string.IsNullOrWhiteSpace(p) ? null : Environment.ExpandEnvironmentVariables(p.Trim().Trim('"'));

    private static string? NewestDesktopCopy(string root)
    {
        try
        {
            if (!Directory.Exists(root)) return null;
            var best = Directory.EnumerateFiles(root, "claude.exe", SearchOption.AllDirectories)
                .Where(f => f.Contains($"{Path.DirectorySeparatorChar}claude-code{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(f => (file: f, version: VersionOf(f)))
                .OrderByDescending(x => x.version)
                .ThenByDescending(x => File.GetLastWriteTimeUtc(x.file))
                .FirstOrDefault();
            return best.file;
        }
        catch { return null; }
    }

    /// <summary>The version is the folder right under "claude-code".</summary>
    private static Version VersionOf(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar);
        int i = Array.FindIndex(parts, p => p.Equals("claude-code", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 < parts.Length && Version.TryParse(parts[i + 1], out var v)) return v;
        return new Version(0, 0);
    }

    /// <summary>Claude Code keeps transcripts in ~/.claude/projects/&lt;folder with non-alphanumerics as '-'&gt;.</summary>
    public static bool HasConversations(string projectFolder)
    {
        try
        {
            string key = new string(Path.GetFullPath(projectFolder).TrimEnd('\\')
                .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects", key);
            return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.jsonl").Any();
        }
        catch { return false; }
    }
}
