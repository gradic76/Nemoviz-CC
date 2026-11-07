using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeChat;

/// <summary>
/// The conversations Claude Code keeps for a folder, newest first, each with a readable label:
/// its title (set in the desktop app or by Claude Code), else the first thing the person wrote.
/// Only the head of each file is read (sessions grow to hundreds of MB) and the result is cached
/// per file and modification time, so opening the menu stays instant.
/// </summary>
public static class SessionCatalog
{
    public sealed record Session(string Id, string Label, DateTime When);

    private sealed record Cached(DateTime Stamp, long Length, string? Label, DateTime When);
    private static readonly Dictionary<string, Cached> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex Reminder = new(@"<system-reminder>.*?</system-reminder>", RegexOptions.Singleline | RegexOptions.Compiled);

    private const int HeadBytes = 2 * 1024 * 1024;

    public static List<Session> For(string projectFolder, int max)
    {
        var list = new List<Session>();
        string dir = TranscriptReader.FolderFor(projectFolder);
        if (!Directory.Exists(dir)) return list;

        foreach (var file in new DirectoryInfo(dir).GetFiles("*.jsonl"))
        {
            var (label, when) = Describe(file);
            if (label == null) continue; // nothing a person wrote: probes, aborted starts
            list.Add(new Session(Path.GetFileNameWithoutExtension(file.Name), label, when));
        }
        return list.OrderByDescending(s => s.When).Take(max).ToList();
    }

    private static readonly Regex Timestamp = new("\"timestamp\":\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>
    /// When the conversation last had a message: the newest "timestamp" near the end of the file.
    /// The file's own date is not used, because tools rewrite whole folders of transcripts at once.
    /// </summary>
    private static DateTime LastActivity(FileInfo file)
    {
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0, stream.Length - 256 * 1024);
            stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[stream.Length - start];
            int read = stream.Read(buffer, 0, buffer.Length);
            var matches = Timestamp.Matches(Encoding.UTF8.GetString(buffer, 0, read));
            for (int i = matches.Count - 1; i >= 0; i--)
                if (DateTime.TryParse(matches[i].Groups[1].Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
                    return t.ToLocalTime();
        }
        catch (IOException) { }
        return file.LastWriteTime;
    }

    private static (string? Label, DateTime When) Describe(FileInfo file)
    {
        if (Cache.TryGetValue(file.FullName, out var c) && c.Stamp == file.LastWriteTimeUtc && c.Length == file.Length)
            return (c.Label, c.When);
        string? label = LabelOf(file);
        DateTime when = label == null ? file.LastWriteTime : LastActivity(file);
        Cache[file.FullName] = new Cached(file.LastWriteTimeUtc, file.Length, label, when);
        return (label, when);
    }

    private static string? LabelOf(FileInfo file)
    {

        string? title = null, first = null;
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null && stream.Position <= HeadBytes)
            {
                if (line.Length == 0 || line[0] != '{') continue;
                bool isTitle = line.Contains("\"custom-title\"") || line.Contains("\"type\":\"summary\"");
                if (!isTitle && (first != null || !line.Contains("\"type\":\"user\""))) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string type = Str(root, "type");
                    if (type == "custom-title") title = Str(root, "customTitle") is { Length: > 0 } t ? t : title;
                    else if (type == "summary" && title == null) title = Str(root, "summary") is { Length: > 0 } s ? s : null;
                    else if (type == "user" && first == null && !Bool(root, "isMeta") && !Bool(root, "isSidechain"))
                        first = FirstText(root);
                }
                catch (JsonException) { }
            }
        }
        catch (IOException) { return null; }

        string? label = title ?? first;
        if (label != null)
        {
            label = Regex.Replace(label, @"\s+", " ").Trim();
            if (label.Length > 60) label = label[..60].TrimEnd() + "…";
        }
        return label;
    }

    private static string? FirstText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var msg) || !msg.TryGetProperty("content", out var content)) return null;
        string text;
        if (content.ValueKind == JsonValueKind.String) text = content.GetString() ?? "";
        else if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var b in content.EnumerateArray())
            {
                string bt = Str(b, "type");
                if (bt == "tool_result") return null;
                if (bt == "text") sb.Append(Str(b, "text")).Append(' ');
            }
            text = sb.ToString();
        }
        else return null;

        text = Reminder.Replace(text, "").Trim();
        if (text.Length == 0 || text.StartsWith('<') || text.StartsWith("[Request interrupted", StringComparison.Ordinal)
            || text.StartsWith("Caveat:", StringComparison.Ordinal))
            return null;
        return text;
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
