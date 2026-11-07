using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeChat;

/// <summary>Window geometry, as ratios of the monitor's work area (taskbar excluded).</summary>
public sealed class WindowSettings
{
    public double WidthRatio { get; set; } = 1.0 / 3.0;
    public double HeightRatio { get; set; } = 1.0;
    /// <summary>Reserve below the window, in device-independent pixels (scaled by DPI).</summary>
    public int BottomReserve { get; set; } = 10;
    /// <summary>"right", "left" or "center".</summary>
    public string Dock { get; set; } = "right";
}

/// <summary>
/// All settings in one place. Layers, each overriding the previous one:
/// built-in defaults → user file → project file (.claude-chat.json in the project folder) → command line.
/// The user file is settings.json next to the exe (portable) or %APPDATA%\ClaudeChat\settings.json.
/// </summary>
public sealed class Settings
{
    // --- Branding / integration (for other users and other projects) ---
    public string AppName { get; set; } = "Nemoviz CC";
    public string AssistantName { get; set; } = "Claude";
    /// <summary>Null = the localized word for "you".</summary>
    public string? UserName { get; set; }

    // --- Claude Code ---
    /// <summary>Explicit path to claude.exe; null = discover it (see ClaudeLocator).</summary>
    public string? ClaudePath { get; set; }
    /// <summary>Extra command-line arguments passed to claude.exe as they are.</summary>
    public List<string> ClaudeArgs { get; set; } = new();
    /// <summary>Extra environment variables for claude.exe.</summary>
    public Dictionary<string, string> ClaudeEnv { get; set; } = new();
    public string? Model { get; set; }
    /// <summary>low, medium, high, xhigh, max; null = Claude Code's default.</summary>
    public string? Effort { get; set; }
    /// <summary>Default "auto": with no permission dialog yet, any other default refuses every edit.</summary>
    public string? PermissionMode { get; set; } = "auto";
    public string? ProjectFolder { get; set; }
    /// <summary>
    /// The folder whose subfolders are the Code projects (File → Code). Null = the parent of the
    /// current project folder (on the author's machine D:\Claude).
    /// </summary>
    public string? ProjectsRoot { get; set; }
    /// <summary>Subfolder of ProjectsRoot for plain conversations (File → Chat); not a project.</summary>
    public string ChatFolder { get; set; } = "Chat";
    /// <summary>"continue" (resume the last conversation) or "new".</summary>
    public string StartMode { get; set; } = "continue";
    /// <summary>
    /// Launcher to run when a restart is requested (%APPDATA%\ClaudeChat\restart.request);
    /// it is called with "--restart" and must wait for this window to close. Null = no restarts.
    /// </summary>
    public string? RestartCommand { get; set; }

    // --- Usage limit ---
    public bool ResumeAfterLimit { get; set; }
    /// <summary>Look for a newer release at start (at most once a day).</summary>
    public bool CheckUpdates { get; set; } = true;
    /// <summary>Null = the localized default prompt.</summary>
    public string? ResumePrompt { get; set; }
    public int LimitRetryMinutes { get; set; } = 15;
    /// <summary>
    /// At this context fill (%) the conversation hands over to a new one before Claude Code
    /// compacts it: memory, CLAUDE.md, commit and a hand-over file, then a fresh session reads
    /// them. 0 = off.
    /// </summary>
    public int ContextHandoffPercent { get; set; } = 95;
    /// <summary>Wait this long after the announced reset before resuming.</summary>
    public int LimitResumeDelaySeconds { get; set; } = 60;

    // --- Look and language ---
    /// <summary>"auto" = Windows UI language, otherwise a file name in lang\ (e.g. "hr", "en").</summary>
    public string Language { get; set; } = "auto";
    /// <summary>"system", "light" or "dark". High contrast always wins.</summary>
    public string Theme { get; set; } = "system";
    public double FontScale { get; set; } = 1.0;
    public WindowSettings Window { get; set; } = new();

    // --- Speech ---
    /// <summary>"auto", "jaws", "nvda" or "none".</summary>
    public string Speech { get; set; } = "auto";
    // --- Sounds (sounds\Send.wav, Bell.wav, Typing.wav next to the exe) ---
    public bool SoundsEnabled { get; set; } = true;
    /// <summary>When a message is sent.</summary>
    public bool SoundSend { get; set; } = true;
    /// <summary>When an answer is complete.</summary>
    public bool SoundBell { get; set; } = true;
    /// <summary>Looped while Claude works and Live does not have the focus.</summary>
    public bool SoundTyping { get; set; } = true;
    /// <summary>0–100: the master volume of all sounds.</summary>
    public int SoundVolume { get; set; } = 80;
    /// <summary>0–100 each, on top of the master volume (author, 2026-11-07).</summary>
    public int SoundSendVolume { get; set; } = 100;
    public int SoundBellVolume { get; set; } = 100;
    public int SoundTypingVolume { get; set; } = 100;
    /// <summary>Keep the output device awake between sounds (HDMI / TVs cut off the start otherwise).</summary>
    public bool KeepAudioDeviceAwake { get; set; }
    /// <summary>Output device name as Windows lists it; null = the default device.</summary>
    public string? AudioDevice { get; set; }

    /// <summary>Appended to Claude Code's system prompt through this window; null = the built-in
    /// plain-text note, "" = nothing.</summary>
    public string? WindowSystemPrompt { get; set; }

    /// <summary>Speak a message when it is sent (or queued).</summary>
    public bool SpeakSent { get; set; } = true;
    /// <summary>Speak an answer when it is complete (not when Live has the focus: it was heard there).</summary>
    public bool SpeakReplies { get; set; } = true;

    // ---------------------------------------------------------------------

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The file user-level changes (theme, language, check box) are written to.</summary>
    public string UserFile { get; private set; } = "";
    public List<string> LoadedFiles { get; } = new();
    public List<string> Problems { get; } = new();

    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeChat");

    public static string ResolveUserFile(string? explicitConfig)
    {
        if (!string.IsNullOrWhiteSpace(explicitConfig)) return Path.GetFullPath(explicitConfig);
        string portable = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (File.Exists(portable)) return portable;
        return Path.Combine(AppDataFolder, "settings.json");
    }

    public static string ProjectFileFor(string projectFolder) => Path.Combine(projectFolder, ".claude-chat.json");

    /// <summary>Loads defaults + user file + (optional) project file + command-line overrides.</summary>
    public static Settings Load(string userFile, string? projectFolder, JsonObject? commandLine)
    {
        var merged = JsonSerializer.SerializeToNode(new Settings(), JsonOptions)!.AsObject();
        var loaded = new List<string>();
        var problems = new List<string>();

        void Layer(string file)
        {
            if (!File.Exists(file)) return;
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(file), NodeOptions, DocOptions);
                if (node is JsonObject obj) { Merge(merged, obj); loaded.Add(file); }
            }
            catch (Exception ex) { problems.Add($"{file}: {ex.Message}"); }
        }

        Layer(userFile);
        if (!string.IsNullOrEmpty(projectFolder)) Layer(ProjectFileFor(projectFolder));
        if (commandLine != null) Merge(merged, commandLine);

        Settings s;
        try { s = merged.Deserialize<Settings>(JsonOptions) ?? new Settings(); }
        catch (Exception ex) { problems.Add(ex.Message); s = new Settings(); }
        s.UserFile = userFile;
        s.LoadedFiles.AddRange(loaded);
        s.Problems.AddRange(problems);
        return s;
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            string? existing = FindKey(target, key);
            if (value is JsonObject so && existing != null && target[existing] is JsonObject to)
                Merge(to, so);
            else
            {
                if (existing != null) target.Remove(existing);
                target[ToCamel(key)] = value?.DeepClone();
            }
        }
    }

    private static string? FindKey(JsonObject obj, string key)
    {
        foreach (var (k, _) in obj)
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return k;
        return null;
    }

    private static string ToCamel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];

    /// <summary>Writes one value into the user file, keeping everything else in it.</summary>
    public void SaveUserValue(string key, JsonNode? value) => SaveValue(UserFile, key, value);

    /// <summary>
    /// Writes a value where it takes effect: into the project file when that file sets the same
    /// key (it would otherwise override the change), else into the user file.
    /// </summary>
    public void SaveEffectiveValue(string? projectFolder, string key, JsonNode? value)
    {
        if (!string.IsNullOrEmpty(projectFolder))
        {
            string pf = ProjectFileFor(projectFolder);
            try
            {
                if (File.Exists(pf) && JsonNode.Parse(File.ReadAllText(pf), NodeOptions, DocOptions) is JsonObject o
                    && FindKey(o, key) != null)
                {
                    SaveValue(pf, key, value);
                    return;
                }
            }
            catch { }
        }
        SaveValue(UserFile, key, value);
    }

    private void SaveValue(string file, string key, JsonNode? value)
    {
        try
        {
            JsonObject root = new();
            if (File.Exists(file))
            {
                var node = JsonNode.Parse(File.ReadAllText(file), NodeOptions, DocOptions);
                if (node is JsonObject obj) root = obj;
            }
            string? existing = FindKey(root, key);
            if (existing != null) root.Remove(existing);
            if (value != null) root[key] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, root.ToJsonString(JsonOptions));
        }
        catch (Exception ex) { Problems.Add($"{file}: {ex.Message}"); }
    }
}

/// <summary>What the app remembers between runs (not settings): last project, last session per project.</summary>
public sealed class AppState
{
    public string? LastProject { get; set; }
    public DateTime? LastUpdateCheck { get; set; }
    /// <summary>A release the user said No to: not offered again at start (Help still offers it).</summary>
    public string? SkippedVersion { get; set; }
    public Dictionary<string, string> Sessions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Unsent text of the Message field per project, kept when the window closes.</summary>
    public Dictionary<string, string> Drafts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Per conversation (session id): title, own instructions, model, effort — set by New chat.</summary>
    public Dictionary<string, ConversationMeta> Conversations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Project folders taken out of the Code list without deleting their files.</summary>
    public List<string> HiddenProjects { get; set; } = new();
    /// <summary>Project folders outside the projects folder, opened by hand (Code list).</summary>
    public List<string> ExtraProjects { get; set; } = new();

    private static string FilePath => Path.Combine(Settings.AppDataFolder, "state.json");

    public static AppState Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Settings.JsonOptions) ?? new AppState();
        }
        catch { /* a broken state file only costs the remembered session */ }
        return new AppState();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.AppDataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Settings.JsonOptions));
        }
        catch { }
    }
}
