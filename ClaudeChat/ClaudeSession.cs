using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ClaudeChat;

public sealed record ResultInfo(
    bool IsError,
    string Subtype,
    string Text,
    string? SessionId,
    IReadOnlyList<string> Denials,
    bool UsageLimit,
    DateTimeOffset? ResetsAt);

/// <summary>
/// One claude.exe process in print mode with stream-json on both ends.
/// Each line on stdout is one JSON record; each user message is one JSON line on stdin.
/// Every event is raised through <see cref="_post"/>, i.e. on the UI thread.
/// </summary>
public sealed class ClaudeSession : IDisposable
{
    private readonly Action<Action> _post;
    private Process? _process;
    private volatile Process? _killedProcess;
    private StreamWriter? _stdin;
    private readonly object _writeLock = new();
    private readonly LinkedList<string> _stderrTail = new();
    private readonly Dictionary<string, string> _toolNames = new();
    private DateTimeOffset? _lastResetsAt;
    private bool _lastLimitRejected;
    private int _requestCounter;

    public string? SessionId { get; private set; }
    public bool IsRunning => _process is { HasExited: false };

    public event Action<string, string>? Initialized;          // sessionId, model
    public event Action<string>? TextDelta;                     // streamed text
    public event Action<string>? AssistantText;                 // one complete text block
    public event Action<string, string>? ToolStarted;           // tool name, summary
    public event Action<string, bool>? ToolFinished;            // tool name, isError
    public event Action<ResultInfo>? Result;
    public event Action<int, string>? Exited;                   // exit code, stderr tail
    /// <summary>5-hour and 7-day usage (0..1) with their reset times; null when not reported.</summary>
    public event Action<double?, DateTimeOffset?, double?, DateTimeOffset?>? Limits;
    /// <summary>Tokens in the context after the last request, and the model's context window.</summary>
    public event Action<long, long>? Context;

    public ClaudeSession(Action<Action> post) { _post = post; }

    public void Start(string exe, string workingDir, IEnumerable<string> args, IDictionary<string, string> env)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        psi.Arguments = Compat.JoinArguments(args);
        foreach (var (k, v) in env) psi.Environment[k] = v;

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.Start();
        _process = p;
        // ProcessStartInfo has no input encoding here: without this, Croatian letters reach Claude as ANSI.
        _stdin = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

        new Thread(() => ReadStdout(p)) { IsBackground = true, Name = "claude-stdout" }.Start();
        new Thread(() => ReadStderr(p)) { IsBackground = true, Name = "claude-stderr" }.Start();
    }

    public void SendUserMessage(string text)
    {
        var msg = new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
        };
        WriteLine(msg.ToJsonString());
    }

    /// <summary>Asks Claude Code to stop the current turn (SDK control request).</summary>
    public void Interrupt()
    {
        var msg = new JsonObject
        {
            ["type"] = "control_request",
            ["request_id"] = $"cc-{Interlocked.Increment(ref _requestCounter)}",
            ["request"] = new JsonObject { ["subtype"] = "interrupt" },
        };
        WriteLine(msg.ToJsonString());
    }

    private void WriteLine(string line)
    {
        lock (_writeLock)
        {
            try { _stdin?.WriteLine(line); }
            catch (IOException) { /* process gone; Exited reports it */ }
            catch (ObjectDisposedException) { }
        }
    }

    public void Kill()
    {
        var p = _process;
        _process = null;
        if (p == null) return;
        _killedProcess = p; // a deliberate stop is not reported as an unexpected exit
        try { _stdin?.Close(); } catch { }
        try { if (!p.HasExited) Compat.KillTree(p); } catch { }
    }

    public void Dispose() => Kill();

    // ------------------------------------------------------------------ reading

    private void ReadStdout(Process p)
    {
        try
        {
            string? line;
            while ((line = p.StandardOutput.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                JsonNode? node;
                try { node = JsonNode.Parse(line); }
                catch (JsonException) { continue; }
                if (node is JsonObject obj) Handle(obj);
            }
        }
        catch { }

        try { p.WaitForExit(); } catch { }
        int code = 0;
        try { code = p.ExitCode; } catch { }
        string tail;
        lock (_stderrTail) tail = string.Join(Environment.NewLine, _stderrTail);
        if (!ReferenceEquals(p, _killedProcess))
            _post(() => Exited?.Invoke(code, tail));
    }

    private void ReadStderr(Process p)
    {
        try
        {
            string? line;
            while ((line = p.StandardError.ReadLine()) != null)
                lock (_stderrTail)
                {
                    _stderrTail.AddLast(line);
                    while (_stderrTail.Count > 20) _stderrTail.RemoveFirst();
                }
        }
        catch { }
    }

    private void Handle(JsonObject obj)
    {
        string type = Str(obj, "type");
        switch (type)
        {
            case "system" when Str(obj, "subtype") == "init":
            {
                string id = Str(obj, "session_id");
                string model = Str(obj, "model");
                _post(() => { SessionId = id; Initialized?.Invoke(id, model); });
                break;
            }

            case "stream_event":
            {
                var ev = obj["event"] as JsonObject;
                if (ev != null && Str(ev, "type") == "content_block_delta"
                    && ev["delta"] is JsonObject delta && Str(delta, "type") == "text_delta")
                {
                    string text = Str(delta, "text");
                    if (text.Length > 0) _post(() => TextDelta?.Invoke(text));
                }
                break;
            }

            case "assistant":
            {
                if (obj["message"]?["content"] is JsonArray content)
                    foreach (var item in content)
                    {
                        if (item is not JsonObject block) continue;
                        switch (Str(block, "type"))
                        {
                            case "text":
                                string t = Str(block, "text");
                                if (t.Length > 0) _post(() => AssistantText?.Invoke(t));
                                break;
                            case "tool_use":
                                string name = Str(block, "name");
                                string id = Str(block, "id");
                                string summary = Summarize(name, block["input"] as JsonObject);
                                _post(() => { _toolNames[id] = name; ToolStarted?.Invoke(name, summary); });
                                break;
                        }
                    }
                break;
            }

            case "user":
            {
                if (obj["message"]?["content"] is JsonArray content)
                    foreach (var item in content)
                        if (item is JsonObject block && Str(block, "type") == "tool_result")
                        {
                            string id = Str(block, "tool_use_id");
                            bool isError = block["is_error"]?.GetValueKind() == JsonValueKind.True;
                            _post(() =>
                            {
                                string name = _toolNames.TryGetValue(id, out var n) ? n : "?";
                                ToolFinished?.Invoke(name, isError);
                            });
                        }
                break;
            }

            case "rate_limit_event":
            {
                // { rate_limit_info: { status: "allowed" | "allowed_warning" | "rejected", resetsAt: <epoch s> } }
                var info = obj["rate_limit_info"] as JsonObject ?? obj;
                var reset = FindEpoch(info);
                string status = Str(info, "status");
                // unifiedWindows: { five_hour: { utilization: 0.85, resetsAt }, seven_day: { … } }
                var windows = info["unifiedWindows"] as JsonObject;
                var (five, fiveAt) = Window(windows?["five_hour"] as JsonObject);
                var (week, weekAt) = Window(windows?["seven_day"] as JsonObject);
                _post(() =>
                {
                    if (reset != null) _lastResetsAt = reset;
                    _lastLimitRejected = status == "rejected";
                    if (five != null || week != null) Limits?.Invoke(five, fiveAt, week, weekAt);
                });
                break;
            }

            case "result":
            {
                bool isError = obj["is_error"]?.GetValueKind() == JsonValueKind.True;
                string subtype = Str(obj, "subtype");
                string text = Str(obj, "result");
                if (text.Length == 0 && obj["errors"] is JsonArray errs) text = string.Join(" ", errs);
                string sid = Str(obj, "session_id");

                var denials = new List<string>();
                if (obj["permission_denials"] is JsonArray arr)
                    foreach (var d in arr)
                        if (d is JsonObject dj)
                        {
                            string tool = Str(dj, "tool_name");
                            string what = Summarize(tool, dj["tool_input"] as JsonObject);
                            denials.Add(what.Length > 0 ? $"{tool} ({what})" : tool);
                        }

                var (used, window) = ContextOf(obj);
                if (window > 0) _post(() => Context?.Invoke(used, window));

                bool limitByText = isError && LimitDetector.LooksLikeUsageLimit(text);
                var resetFromText = limitByText ? LimitDetector.ParseReset(text, DateTimeOffset.Now) : null;

                _post(() =>
                {
                    bool limit = limitByText || (isError && _lastLimitRejected);
                    var resets = resetFromText ?? (limit ? _lastResetsAt : null);
                    if (sid.Length > 0) SessionId = sid;
                    Result?.Invoke(new ResultInfo(isError, subtype, text, sid.Length > 0 ? sid : null, denials, limit, resets));
                    _lastLimitRejected = false;
                });
                break;
            }
        }
    }

    private static string Str(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : "";

    private static (double?, DateTimeOffset?) Window(JsonObject? w)
    {
        if (w == null) return (null, null);
        double? u = w["utilization"] is JsonValue v && v.TryGetValue<double>(out double d) ? d : null;
        return (u, FindEpoch(w));
    }

    /// <summary>
    /// The context after the turn = what the last request sent (fresh, cached and newly cached
    /// input) plus what it answered; the window comes from modelUsage.
    /// </summary>
    private static (long used, long window) ContextOf(JsonObject result)
    {
        long N(JsonNode? n, string k) => n?[k] is JsonValue v && v.TryGetValue<long>(out long x) ? x : 0;
        var usage = result["usage"] as JsonObject;
        JsonNode? last = usage?["iterations"] is JsonArray it && it.Count > 0 ? it[it.Count - 1] : usage;
        long used = N(last, "input_tokens") + N(last, "cache_read_input_tokens")
                  + N(last, "cache_creation_input_tokens") + N(last, "output_tokens");
        long window = 0;
        if (result["modelUsage"] is JsonObject models)
            foreach (var (_, m) in models) window = Math.Max(window, N(m, "contextWindow"));
        return (used, window);
    }

    private static DateTimeOffset? FindEpoch(JsonObject obj)
    {
        foreach (var key in new[] { "resetsAt", "resets_at", "reset_at", "resetAt" })
            if (obj[key] is JsonValue v)
            {
                if (v.TryGetValue<long>(out long s)) return DateTimeOffset.FromUnixTimeSeconds(s > 10_000_000_000 ? s / 1000 : s);
                if (v.TryGetValue<double>(out double d)) return DateTimeOffset.FromUnixTimeSeconds((long)d);
            }
        return null;
    }

    /// <summary>One short line about what a tool is doing, for the Live field.</summary>
    private static string Summarize(string tool, JsonObject? input)
    {
        if (input == null) return "";
        string[] preferred = tool switch
        {
            "Bash" or "PowerShell" => ["command"],
            "Read" or "Edit" or "Write" or "NotebookEdit" => ["file_path", "notebook_path"],
            "Grep" or "Glob" => ["pattern"],
            "WebFetch" => ["url"],
            "WebSearch" => ["query"],
            "Agent" or "Task" => ["description"],
            _ => ["description", "command", "file_path", "path", "url", "query", "pattern"],
        };
        foreach (var key in preferred)
            if (input[key] is JsonValue v && v.GetValueKind() == JsonValueKind.String)
                return Shorten(v.GetValue<string>());
        foreach (var (_, value) in input)
            if (value is JsonValue v && v.GetValueKind() == JsonValueKind.String)
                return Shorten(v.GetValue<string>());
        return "";
    }

    private static string Shorten(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 140 ? s.Substring(0, 140) + "…" : s;
    }
}
