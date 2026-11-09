using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeChat;

/// <summary>
/// Reads the conversation Claude Code keeps on disk
/// (~/.claude/projects/&lt;folder key&gt;/&lt;session id&gt;.jsonl) so History opens with the
/// earlier messages. Only what a person said and what Claude answered is kept: tool calls,
/// tool results, meta notes, side chains and system reminders are left out, and one answer
/// (several text blocks between tool calls) becomes one entry, as it does live.
/// </summary>
public static class TranscriptReader
{
    public enum Speaker { User, Assistant }
    public sealed record Message(Speaker Who, DateTime Time, string Text);

    private static readonly Regex Reminder = new(@"<system-reminder>.*?</system-reminder>", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string FolderFor(string projectFolder)
    {
        string key = new string(Path.GetFullPath(projectFolder).TrimEnd('\\')
            .Select(c => Compat.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects", key);
    }

    /// <summary>The given session's file, or else the newest one (what --continue would pick).</summary>
    public static string? FindFile(string projectFolder, string? sessionId)
    {
        try
        {
            string dir = FolderFor(projectFolder);
            if (!Directory.Exists(dir)) return null;
            if (!string.IsNullOrEmpty(sessionId))
            {
                string f = Path.Combine(dir, sessionId + ".jsonl");
                if (File.Exists(f)) return f;
            }
            return new DirectoryInfo(dir).GetFiles("*.jsonl")
                .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    public static List<Message> Load(string file, string imageNote, int maxMessages)
    {
        var result = Read(file, imageNote, maxMessages, skipByText: true);
        // Nothing found: perhaps the file's format changed and the shortcut no longer fits.
        return result.Count > 0 ? result : Read(file, imageNote, maxMessages, skipByText: false);
    }

    /// <summary>Most of a long transcript is tool results and tool calls, which never become
    /// messages. They are recognised in the raw text, which costs far less than parsing them.
    /// In text inside JSON strings the quotes are escaped, so these patterns only match real fields.</summary>
    private static bool MayHoldMessage(string line)
    {
        if (line.Contains("\"type\":\"user\"") && !line.Contains("\"type\":\"tool_result\"")) return true;
        return line.Contains("\"type\":\"assistant\"") && line.Contains("\"type\":\"text\"");
    }

    private static List<Message> Read(string file, string imageNote, int maxMessages, bool skipByText)
    {
        var result = new List<Message>();
        var answer = new StringBuilder();
        DateTime answerTime = DateTime.MinValue;

        void FlushAnswer()
        {
            if (answer.Length > 0) result.Add(new Message(Speaker.Assistant, answerTime, answer.ToString()));
            answer.Clear();
        }

        // Claude Code may be writing the file right now.
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] != '{') continue;
            if (skipByText && !MayHoldMessage(line)) continue;
            JsonNode? root;
            try { root = JsonNode.Parse(line); }
            catch (JsonException) { continue; }

            string type = Str(root, "type");
            if (type != "user" && type != "assistant") continue;
            if (Bool(root, "isMeta") || Bool(root, "isSidechain") || Bool(root, "isCompactSummary")) continue;
            var content = root?["message"]?["content"];
            if (content == null) continue;
            DateTime time = DateTime.TryParse(Str(root, "timestamp"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
                ? t.ToLocalTime() : DateTime.Now;

            if (type == "assistant")
            {
                if (content is not JsonArray blocks) continue;
                foreach (var block in blocks)
                    if (Str(block, "type") == "text")
                    {
                        string text = Str(block, "text").Trim();
                        if (text.Length == 0) continue;
                        if (answer.Length > 0) answer.Append("\n\n");
                        answer.Append(text);
                        answerTime = time;
                    }
                continue;
            }

            // user: a typed message is a string, or text/image blocks; tool results are not messages.
            string said = UserText(content, imageNote);
            if (said.Length == 0) continue;
            FlushAnswer();
            result.Add(new Message(Speaker.User, time, said));
        }
        FlushAnswer();
        return result.Count > maxMessages ? result.GetRange(result.Count - maxMessages, maxMessages) : result;
    }

    private static string UserText(JsonNode content, string imageNote)
    {
        var parts = new List<string>();
        if (content is JsonValue v && v.GetValueKind() == JsonValueKind.String) parts.Add(v.GetValue<string>());
        else if (content is JsonArray blocks)
            foreach (var block in blocks)
            {
                string bt = Str(block, "type");
                if (bt == "tool_result") return "";
                if (bt == "text") parts.Add(Str(block, "text"));
                else if (bt == "image") parts.Add(imageNote);
            }

        string text = Reminder.Replace(string.Join("\n", parts), "").Trim();
        // Interruption notes and slash-command plumbing are not something the person wrote.
        if (text.StartsWith("[Request interrupted", StringComparison.Ordinal)
            || text.StartsWith("<command-", StringComparison.Ordinal)
            || text.StartsWith("<local-command", StringComparison.Ordinal)
            || text.StartsWith("Caveat:", StringComparison.Ordinal))
            return "";
        return text;
    }

    private static string Str(JsonNode? e, string name) =>
        e?[name] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : "";

    private static bool Bool(JsonNode? e, string name) => e?[name]?.GetValueKind() == JsonValueKind.True;
}
