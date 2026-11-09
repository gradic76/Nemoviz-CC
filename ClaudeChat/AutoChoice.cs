using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClaudeChat;

/// <summary>
/// "Automatic" in the Model and Effort boxes (author, 2026-11-09): before a message goes out, a
/// short one-off Haiku call rates how demanding it is, and the conversation continues with the
/// model and effort that fit. About 2 seconds and a few hundred tokens, measured.
/// Effort can change freely. A new model cannot reuse the prompt cache (measured: the whole
/// context is written again), so in a long conversation the model only ever goes up.
/// Fable runs on credits only and is deliberately not among the choices.
/// </summary>
internal sealed partial class MainWindow
{
    private const string AutoChoice = "auto";
    private const int EstimatorTimeoutMs = 20_000;
    /// <summary>Above this many tokens of context a weaker model is not worth a rewritten cache.</summary>
    private const long ModelDowngradeContextLimit = 100_000;

    private static readonly string[] AutoModels = { "haiku", "sonnet", "opus" };
    private static readonly string[] AutoEfforts = { "low", "medium", "high", "xhigh", "max" };

    private const string EstimatorRules =
        "You choose the model and effort for the next turn of a coding assistant. Messages are often in Croatian. " +
        "Do not answer the message and do not explain. Reply with one line of JSON only: {\"model\":\"...\",\"effort\":\"...\"}\n" +
        "model:\n" +
        "- haiku: small talk, a simple factual question, a short lookup, a tiny mechanical edit.\n" +
        "- sonnet: the default for real work: explanations, writing, a bug whose place is known, changes in a few files.\n" +
        "- opus: only when sonnet would likely fall short: architecture, a bug with no known cause, migrations, security, long multi-step tasks.\n" +
        "effort: low for trivial, medium for ordinary, high when care is needed, xhigh for hard problems, max only for the hardest.\n" +
        "Use \"same\" for both when the message is a short reaction or go-ahead that only makes sense with the earlier work " +
        "(for example: ok, go on, može, nastavi, probaj). Between haiku and sonnet, when unsure, choose sonnet; between sonnet and opus, when unsure, choose sonnet.";

    private bool AutoChoiceOn => _settings.Model == AutoChoice || _settings.Effort == AutoChoice;

    // What the estimate last chose (null until the first one), and what the running process was started with.
    private string? _autoModel, _autoEffort, _sessionModel, _sessionEffort;
    private long _contextTokens;
    private string? _estimateText;
    private int _estimateId;
    private volatile Process? _estimator;

    private string? EffectiveModel => _settings.Model == AutoChoice ? _autoModel : _settings.Model;
    private string? EffectiveEffort => _settings.Effort == AutoChoice ? _autoEffort : _settings.Effort;

    private void ResetAutoChoice()
    {
        _autoModel = _autoEffort = null;
        _contextTokens = 0;
    }

    /// <summary>Rates the message in the background, then sends it. Stop still works meanwhile.</summary>
    private void EstimateThenSend(string text)
    {
        _claudeExe ??= ClaudeLocator.Find(_settings.ClaudePath);
        if (_claudeExe == null) { Dispatch(text); return; } // EnsureSession reports what is missing

        CancelResumeTimer();
        _busy = true;
        _estimateText = text;
        _liveLine.Clear();
        SetLive(_loc["live.estimating"]);

        string exe = _claudeExe, prompt = EstimatorPrompt(text);
        var env = _settings.ClaudeEnv;
        int id = ++_estimateId;
        Task.Run(() =>
        {
            var (model, effort) = RunEstimator(exe, prompt, env, p => _estimator = p);
            Post(() => OnEstimate(id, text, model, effort));
        });
    }

    private void OnEstimate(int id, string text, string? model, string? effort)
    {
        if (id != _estimateId) return; // called off
        _busy = false;
        _estimateText = null;
        ApplyEstimate(model, effort);
        Dispatch(text);
        if (AutoChoiceOn) AddLive(_loc.Format("live.autoChoice", ModelName(EffectiveModel), EffortName(EffectiveEffort)));
    }

    private string ModelName(string? v) => _loc[v == null ? "choice.default" : "model." + v];
    private string EffortName(string? v) => _loc[v == null ? "choice.default" : "effort." + v];

    private void ApplyEstimate(string? model, string? effort)
    {
        if (_settings.Model == AutoChoice)
        {
            string? wanted = Array.IndexOf(AutoModels, model) >= 0 ? model : _autoModel;
            // A failed estimate (null) leaves Claude Code's own default; "same" with nothing before it does not.
            if (wanted == null && model == "same") wanted = "sonnet";
            bool weaker = wanted != null && _autoModel != null && Array.IndexOf(AutoModels, wanted) < Array.IndexOf(AutoModels, _autoModel);
            if (!(weaker && _contextTokens > ModelDowngradeContextLimit)) _autoModel = wanted;
        }
        if (_settings.Effort == AutoChoice)
        {
            string? wanted = Array.IndexOf(AutoEfforts, effort) >= 0 ? effort : _autoEffort;
            if (wanted == null && effort == "same") wanted = "medium";
            _autoEffort = wanted;
        }
        if (_session is { IsRunning: true } && (_sessionModel != EffectiveModel || _sessionEffort != EffectiveEffort))
        {
            _session.Kill(); // the next start resumes the same conversation with the new choice
            _session = null;
        }
    }

    /// <summary>Stop while the estimate runs: nothing has been sent, so the text goes back to the Message field.</summary>
    private void CancelEstimate()
    {
        _estimateId++;
        KillEstimator();
        _busy = false;
        if (_estimateText != null) _queue.Insert(0, _estimateText);
        _estimateText = null;
        SetLive(_loc["live.stopped"]);
        RestoreQueueToInput();
    }

    private void KillEstimator()
    {
        var p = _estimator;
        if (p == null) return;
        try { if (!p.HasExited) Compat.KillTree(p); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private string EstimatorPrompt(string text)
    {
        static string Tail(string s, int max) => s.Length <= max ? s : "…" + s.Substring(s.Length - max);
        string system = _loc["role.system"];
        var sb = new StringBuilder();
        var earlier = _entries.Where(e => e.Role != system).Reverse().Take(2).Reverse().ToList();
        if (earlier.Count > 0)
        {
            sb.Append("Earlier in the conversation:\n");
            foreach (var e in earlier) sb.Append(e.Role).Append(": ").Append(Tail(e.Text, 700)).Append('\n');
            sb.Append('\n');
        }
        sb.Append("Now in use: ").Append(_autoModel ?? "nothing yet").Append(", effort ").Append(_autoEffort ?? "nothing yet").Append(".\n\n");
        sb.Append("The next message:\n").Append(Tail(text, 3000));
        return sb.ToString();
    }

    /// <summary>The one-off call: no tools, no project files, no settings, nothing kept on disk.
    /// Null for anything that goes wrong; the message is then sent with the previous choice.</summary>
    private static (string? Model, string? Effort) RunEstimator(string exe, string prompt, IDictionary<string, string>? env, Action<Process?>? publish)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, Compat.JoinArguments(new[]
            {
                "-p", "--model", "haiku", "--no-session-persistence", "--tools", "", "--strict-mcp-config",
                "--disable-slash-commands", "--setting-sources", "", "--system-prompt", EstimatorRules, "--output-format", "json",
            }))
            {
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = new UTF8Encoding(false),
            };
            if (env != null) foreach (var (k, v) in env) psi.Environment[k] = v;
            using var p = Process.Start(psi)!;
            publish?.Invoke(p);
            using (var w = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false))) w.Write(prompt);
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(EstimatorTimeoutMs))
            {
                Compat.KillTree(p);
                return (null, null);
            }
            return output.Wait(2000) ? ParseEstimate(output.Result) : (null, null);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException)
        {
            return (null, null);
        }
        finally { publish?.Invoke(null); }
    }

    private static (string? Model, string? Effort) ParseEstimate(string stdout)
    {
        try
        {
            string last = stdout.Split('\n').Last(l => l.Trim().Length > 0);
            var outer = JsonNode.Parse(last);
            if (outer?["is_error"]?.GetValueKind() == JsonValueKind.True) return (null, null);
            var match = Regex.Match(outer?["result"]?.GetValue<string>() ?? "", @"\{[^{}]*\}");
            if (!match.Success) return (null, null);
            var answer = JsonNode.Parse(match.Value);
            return (answer?["model"]?.ToString(), answer?["effort"]?.ToString());
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return (null, null); }
    }
}
