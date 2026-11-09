using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>What one chat carries besides the folder (File → Chat → New chat): title, its own
/// instructions (appended to the system prompt on every start), model and effort.</summary>
public sealed class ConversationMeta
{
    public string? Title { get; set; }
    public string? Instructions { get; set; }
    public string? Model { get; set; }
    public string? Effort { get; set; }
}

public sealed class ChatDraft
{
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string? Model { get; set; }
    public string? Effort { get; set; }
    public List<string> Files { get; set; } = new();
    public string FirstMessage { get; set; } = "";
    public bool Interview { get; set; }
}

public sealed class ProjectDraft
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Kind { get; set; }
    public string WorkLanguage { get; set; } = "";
    public string Technology { get; set; } = "";
    public bool Accessibility { get; set; } = true;
    public int Git { get; set; } = 1;
    public string RepoName { get; set; } = "";
    public bool RepoPublic { get; set; }
    public string? Permission { get; set; }
    public string? Model { get; set; }
    public string? Effort { get; set; }
    public List<string> Files { get; set; } = new();
    public bool CopyFiles { get; set; } = true;
    public int Plan { get; set; }
    public bool Shortcut { get; set; }
}

/// <summary>
/// The wizards (author, 2026-11-07). File → Chat → New chat… is one form; File → Code → New
/// project… two (the answers, then plan, shortcut and a summary). Every form has Confirm, Save for
/// later (the answers come back next time) and Cancel.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmNewChat = 205, IdmNewProject = 206;
    private const int IdWizAdd = 3900, IdWizRemove = 3901, IdWizGit = 3902;

    /// <summary>A new chat's settings until Claude Code tells its session id (then in state).</summary>
    private ConversationMeta? _pendingMeta;

    private static string WizardDraftPath(string kind) => Path.Combine(Settings.AppDataFolder, $"wizard-{kind}.json");

    private static T? LoadWizardDraft<T>(string kind) where T : class
    {
        try { return File.Exists(WizardDraftPath(kind)) ? Json.Read<T>(File.ReadAllText(WizardDraftPath(kind))) : null; }
        catch { return null; }
    }

    private static void SaveWizardDraft<T>(string kind, T draft)
    {
        try
        {
            Directory.CreateDirectory(Settings.AppDataFolder);
            File.WriteAllText(WizardDraftPath(kind), Json.Write(draft));
        }
        catch { }
    }

    private static void DeleteWizardDraft(string kind)
    {
        try { File.Delete(WizardDraftPath(kind)); } catch { }
    }

    private static int ChoiceIndex((string? Value, string Key)[] choices, string? value)
    {
        for (int i = 0; i < choices.Length; i++)
            if (string.Equals(choices[i].Value, value, StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    private string? ChoiceValue((string? Value, string Key)[] choices, IntPtr combo)
    {
        int i = Selected(combo);
        return i >= 0 && i < choices.Length ? choices[i].Value : null;
    }

    private IEnumerable<string> ChoiceTexts((string? Value, string Key)[] choices) => choices.Select(c => NoAmp(_loc[c.Key]));

    private bool WizardBlocked()
    {
        if (!_busy && !_stopping) return false;
        Win32.MessageBeep(0);
        AddLive(_loc["live.busySwitch"]);
        return true;
    }

    // ------------------------------------------------------------------ per-conversation settings

    private ConversationMeta? CurrentMeta()
    {
        if (_pendingMeta != null) return _pendingMeta;
        string? id = CurrentSessionId();
        return id != null && _state.Conversations.TryGetValue(id, out var m) ? m : null;
    }

    /// <summary>A chat's own model and effort replace the folder's while it is open (in memory
    /// only; the settings are read again on every switch).</summary>
    private void ApplyConversationOverrides()
    {
        var m = CurrentMeta();
        if (m == null) return;
        if (m.Model != null) _settings.Model = m.Model;
        if (m.Effort != null) _settings.Effort = m.Effort;
    }

    // ------------------------------------------------------------------ New chat

    private void NewChatWizard()
    {
        if (WizardBlocked()) return;
        var d = LoadWizardDraft<ChatDraft>("chat") ?? new ChatDraft();

        var f = BeginForm(_loc["wiz.chat.title"]);
        IntPtr title = FEdit(f, _loc["wiz.chat.name"], d.Title);
        IntPtr model = FCombo(f, _loc["wiz.model"], ChoiceTexts(ModelChoices), ChoiceIndex(ModelChoices, d.Model));
        IntPtr effort = FCombo(f, _loc["wiz.effort"], ChoiceTexts(EffortChoices), ChoiceIndex(EffortChoices, d.Effort));
        IntPtr files = FList(f, _loc["wiz.files"], d.Files, 4);
        FButtonRow(f, (IdWizAdd, _loc["wiz.add"]), (IdWizRemove, _loc["wiz.remove"]));
        IntPtr first = FEdit(f, _loc["wiz.chat.first"], d.FirstMessage);
        IntPtr interview = FCheck(f, _loc["wiz.chat.interview"], d.Interview);
        FBig(f, _loc["wiz.chat.instructions"], d.Instructions, false);
        FButton(f, FormOk, _loc["wiz.confirm"], true);
        FButton(f, FormSaveLater, _loc["wiz.saveLater"]);
        FButton(f, FormCancel, _loc["wiz.cancel"]);

        f.OnCommand = (id, code) =>
        {
            if (id == IdWizAdd) { if (PickFiles(f.Hwnd, ChatFolderPath) is { } p) ListAdd(files, p); }
            else if (id == IdWizRemove) ListRemoveSelected(files);
            return false;
        };
        f.BeforeClose = r =>
        {
            d.Title = Win32.GetText(title).Trim();
            d.Model = ChoiceValue(ModelChoices, model);
            d.Effort = ChoiceValue(EffortChoices, effort);
            d.Files = ListItems(files);
            d.FirstMessage = Win32.GetText(first).Trim();
            d.Interview = Checked(interview);
            d.Instructions = FromEdit(Win32.GetText(f.Big)).Trim();
        };

        int result = RunForm(f, title);
        if (result == FormSaveLater) { SaveWizardDraft("chat", d); _speech.Speak(_loc["wiz.saved"], true); return; }
        if (result != FormOk) return;
        DeleteWizardDraft("chat");

        var meta = new ConversationMeta
        {
            Title = d.Title.Length > 0 ? d.Title : null,
            Instructions = d.Instructions.Length > 0 ? d.Instructions : null,
            Model = d.Model,
            Effort = d.Effort,
        };
        SwitchTo(ChatFolderPath, null, newConversation: true, meta.Title, meta);
        StartWith(d.Files, d.Interview
            ? _loc["wiz.chat.interviewPrompt"] + (d.FirstMessage.Length > 0 ? "\n\n" + d.FirstMessage : "")
            : d.FirstMessage);
    }

    /// <summary>Puts the files into the Message field's attachments and sends the text, if any;
    /// with no text the conversation waits, attachments ready.</summary>
    private void StartWith(IEnumerable<string> files, string text)
    {
        foreach (var p in files)
            if ((File.Exists(p) || Directory.Exists(p)) && !_attachments.Contains(p, StringComparer.OrdinalIgnoreCase)) _attachments.Add(p);
        Win32.SetWindowTextW(_lblInput, InputLabelText());
        if (text.Trim().Length == 0) return;
        Win32.SetWindowTextW(_input, Crlf(text));
        SendFromInput(false);
    }

    // ------------------------------------------------------------------ New project

    private void NewProjectWizard()
    {
        if (WizardBlocked()) return;
        var d = LoadWizardDraft<ProjectDraft>("project") ?? new ProjectDraft { WorkLanguage = _loc["wiz.proj.langDefault"] };
        int step = 1;
        while (true)
        {
            int r = step == 1 ? ProjectStep1(d) : ProjectStep2(d);
            if (r == FormCancel) return;
            if (r == FormSaveLater) { SaveWizardDraft("project", d); _speech.Speak(_loc["wiz.saved"], true); return; }
            if (r == FormBack) { step = 1; continue; }
            if (step == 1) { step = 2; continue; }
            if (CreateProject(d)) DeleteWizardDraft("project");
            return;
        }
    }

    private string[] KindTexts => Enumerable.Range(0, 5).Select(i => _loc[$"wiz.proj.kind.{i}"]).ToArray();
    private string[] GitTexts => Enumerable.Range(0, 3).Select(i => _loc[$"wiz.proj.git.{i}"]).ToArray();
    private string[] PlanTexts => Enumerable.Range(0, 2).Select(i => _loc[$"wiz.proj.plan.{i}"]).ToArray();

    private int ProjectStep1(ProjectDraft d)
    {
        var f = BeginForm(_loc["wiz.proj.title1"]);
        IntPtr name = FEdit(f, _loc["wiz.proj.name"], d.Name);
        IntPtr kind = FCombo(f, _loc["wiz.proj.kind"], KindTexts, d.Kind);
        IntPtr lang = FEdit(f, _loc["wiz.proj.lang"], d.WorkLanguage);
        IntPtr tech = FEdit(f, _loc["wiz.proj.tech"], d.Technology);
        IntPtr a11y = FCheck(f, _loc["wiz.proj.a11y"], d.Accessibility);
        IntPtr git = FCombo(f, _loc["wiz.proj.git"], GitTexts, d.Git, IdWizGit);
        IntPtr repo = FEdit(f, _loc["wiz.proj.repo"], d.RepoName);
        IntPtr pub = FCheck(f, _loc["wiz.proj.public"], d.RepoPublic);
        var permChoices = PermissionChoicesWithDefault;
        IntPtr perm = FCombo(f, _loc["wiz.proj.perm"], ChoiceTexts(permChoices), ChoiceIndex(permChoices, d.Permission));
        IntPtr model = FCombo(f, _loc["wiz.model"], ChoiceTexts(ModelChoices), ChoiceIndex(ModelChoices, d.Model));
        IntPtr effort = FCombo(f, _loc["wiz.effort"], ChoiceTexts(EffortChoices), ChoiceIndex(EffortChoices, d.Effort));
        IntPtr files = FList(f, _loc["wiz.proj.files"], d.Files, 3);
        FButtonRow(f, (IdWizAdd, _loc["wiz.add"]), (IdWizRemove, _loc["wiz.remove"]));
        IntPtr copy = FCheck(f, _loc["wiz.proj.copy"], d.CopyFiles);
        FBig(f, _loc["wiz.proj.desc"], d.Description, false);
        FButton(f, FormOk, _loc["wiz.confirm"], true);
        FButton(f, FormSaveLater, _loc["wiz.saveLater"]);
        FButton(f, FormCancel, _loc["wiz.cancel"]);

        void GitChanged()
        {
            bool hub = Selected(git) == 2;
            Win32.EnableWindow(repo, hub);
            Win32.EnableWindow(pub, hub);
        }
        GitChanged();

        f.OnCommand = (id, code) =>
        {
            if (id == IdWizAdd) { if (PickFiles(f.Hwnd, ProjectsRoot) is { } p) ListAdd(files, p); }
            else if (id == IdWizRemove) ListRemoveSelected(files);
            else if (id == IdWizGit && code == Win32.CBN_SELCHANGE) GitChanged();
            else if (id == FormOk)
            {
                string? problem = ProjectNameProblem(Win32.GetText(name).Trim());
                if (problem != null)
                {
                    Win32.MessageBoxW(f.Hwnd, problem, _loc["wiz.proj.title1"], Win32.MB_OK | 0x30 /* MB_ICONWARNING */);
                    Win32.SetFocus(name);
                    return true;
                }
            }
            return false;
        };
        f.BeforeClose = r =>
        {
            d.Name = Win32.GetText(name).Trim();
            d.Kind = Math.Max(0, Selected(kind));
            d.WorkLanguage = Win32.GetText(lang).Trim();
            d.Technology = Win32.GetText(tech).Trim();
            d.Accessibility = Checked(a11y);
            d.Git = Math.Max(0, Selected(git));
            d.RepoName = Win32.GetText(repo).Trim();
            d.RepoPublic = Checked(pub);
            d.Permission = ChoiceValue(permChoices, perm);
            d.Model = ChoiceValue(ModelChoices, model);
            d.Effort = ChoiceValue(EffortChoices, effort);
            d.Files = ListItems(files);
            d.CopyFiles = Checked(copy);
            d.Description = FromEdit(Win32.GetText(f.Big)).Trim();
        };
        return RunForm(f, name);
    }

    private (string? Value, string Key)[] PermissionChoicesWithDefault =>
        new (string? Value, string Key)[] { (null, "wiz.proj.permDefault") }.Concat(PermissionChoices).ToArray();

    private string? ProjectNameProblem(string name)
    {
        if (name.Length == 0) return _loc["wiz.proj.errName"];
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ')) return _loc["wiz.proj.errChars"];
        string folder = Path.Combine(ProjectsRoot, name);
        if (Directory.Exists(folder) || File.Exists(folder)) return _loc.Format("wiz.proj.errExists", folder);
        return null;
    }

    private int ProjectStep2(ProjectDraft d)
    {
        var f = BeginForm(_loc["wiz.proj.title2"]);
        IntPtr plan = FCombo(f, _loc["wiz.proj.plan"], PlanTexts, d.Plan);
        IntPtr shortcut = FCheck(f, _loc["wiz.proj.shortcut"], d.Shortcut);
        FBig(f, _loc["wiz.proj.summary"], ProjectSummary(d), true);
        FButton(f, FormBack, _loc["wiz.back"]);
        FButton(f, FormOk, _loc["wiz.confirm"], true);
        FButton(f, FormSaveLater, _loc["wiz.saveLater"]);
        FButton(f, FormCancel, _loc["wiz.cancel"]);
        f.BeforeClose = r =>
        {
            d.Plan = Math.Max(0, Selected(plan));
            d.Shortcut = Checked(shortcut);
        };
        return RunForm(f, plan);
    }

    private string Yes(bool b) => b ? _loc["wiz.yes"] : _loc["wiz.no"];

    private string ProjectSummary(ProjectDraft d)
    {
        var permChoices = PermissionChoicesWithDefault;
        string Choice((string? Value, string Key)[] c, string? v) => NoAmp(_loc[c[ChoiceIndex(c, v)].Key]);
        var sb = new StringBuilder();
        void Line(string key, string value) => sb.Append(_loc[key]).Append(": ").Append(value).Append('\n');
        Line("wiz.proj.name", d.Name);
        Line("wiz.proj.folder", Path.Combine(ProjectsRoot, d.Name));
        Line("wiz.proj.kind", KindTexts[Compat.Clamp(d.Kind, 0, 4)]);
        Line("wiz.proj.lang", d.WorkLanguage);
        Line("wiz.proj.tech", d.Technology.Length > 0 ? d.Technology : _loc["wiz.md.techAuto"]);
        Line("wiz.proj.a11y", Yes(d.Accessibility));
        Line("wiz.proj.git", GitTexts[Compat.Clamp(d.Git, 0, 2)] + (d.Git == 2
            ? $" ({(d.RepoName.Length > 0 ? d.RepoName : d.Name)}, {(d.RepoPublic ? _loc["wiz.proj.publicWord"] : _loc["wiz.proj.privateWord"])})" : ""));
        Line("wiz.proj.perm", Choice(permChoices, d.Permission));
        Line("wiz.model", Choice(ModelChoices, d.Model));
        Line("wiz.effort", Choice(EffortChoices, d.Effort));
        Line("wiz.proj.files", d.Files.Count == 0 ? _loc["wiz.none"] : string.Join(", ", d.Files.Select(Path.GetFileName)));
        if (d.Files.Count > 0) Line("wiz.proj.copy", Yes(d.CopyFiles));
        sb.Append('\n').Append(_loc["wiz.proj.desc"]).Append(":\n").Append(d.Description.Length > 0 ? d.Description : _loc["wiz.none"]).Append('\n');
        return sb.ToString();
    }

    private string ProjectSkeleton(ProjectDraft d, List<string> copied)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(d.Name).Append("\n\n");
        if (d.Description.Length > 0) sb.Append(d.Description).Append("\n\n");
        sb.Append("## ").Append(_loc["wiz.md.basics"]).Append("\n\n");
        sb.Append("- ").Append(_loc["wiz.proj.kind"]).Append(": ").Append(KindTexts[Compat.Clamp(d.Kind, 0, 4)]).Append('\n');
        if (d.WorkLanguage.Length > 0) sb.Append("- ").Append(_loc["wiz.proj.lang"]).Append(": ").Append(d.WorkLanguage).Append('\n');
        sb.Append("- ").Append(_loc["wiz.md.tech"]).Append(": ").Append(d.Technology.Length > 0 ? d.Technology : _loc["wiz.md.techAuto"]).Append('\n');
        sb.Append("- ").Append(_loc["wiz.md.a11y"]).Append(": ").Append(d.Accessibility ? _loc["wiz.md.a11yOn"] : _loc["wiz.md.a11yOff"]).Append('\n');
        sb.Append("- Git: ").Append(GitTexts[Compat.Clamp(d.Git, 0, 2)]).Append('\n');
        if (copied.Count > 0)
        {
            sb.Append("\n## ").Append(_loc["wiz.md.files"]).Append("\n\n");
            foreach (var c in copied) sb.Append("- ").Append(c).Append('\n');
        }
        sb.Append("\n## ").Append(_loc["wiz.md.plan"]).Append("\n\n").Append(_loc["wiz.md.planTodo"]).Append('\n');
        return sb.ToString();
    }

    private bool CreateProject(ProjectDraft d)
    {
        if (ProjectNameProblem(d.Name) is { } problem)
        {
            Win32.MessageBoxW(_hwnd, problem, _loc["wiz.proj.title2"], Win32.MB_OK | 0x30);
            return false;
        }
        string folder = Path.Combine(ProjectsRoot, d.Name);
        var copied = new List<string>();
        var notes = new List<string>();
        try
        {
            Directory.CreateDirectory(folder);
            if (d.CopyFiles)
                foreach (var p in d.Files)
                {
                    if (!File.Exists(p)) { notes.Add(_loc.Format("wiz.proj.missing", p)); continue; }
                    string target = Path.Combine(folder, Path.GetFileName(p));
                    if (File.Exists(target)) continue;
                    File.Copy(p, target);
                    copied.Add(Path.GetFileName(p));
                }
            File.WriteAllText(Path.Combine(folder, "CLAUDE.md"), ProjectSkeleton(d, copied), new UTF8Encoding(false));
            var cfg = new JsonObject();
            if (d.Permission != null) cfg["permissionMode"] = d.Permission;
            if (d.Model != null) cfg["model"] = d.Model;
            if (d.Effort != null) cfg["effort"] = d.Effort;
            if (cfg.Count > 0) File.WriteAllText(Path.Combine(folder, ".claude-chat.json"), cfg.ToJsonString(indented: true), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Win32.MessageBoxW(_hwnd, _loc.Format("wiz.proj.errCreate", e.Message), _loc["wiz.proj.title2"], Win32.MB_OK | 0x10 /* MB_ICONERROR */);
            return false;
        }

        if (d.Git >= 1 && RunQuiet("git", folder, "init") is { } gitErr) notes.Add(_loc.Format("wiz.proj.gitFail", gitErr));
        if (d.Shortcut && CreateProjectShortcut(folder, d.Name) is { } lnkErr) notes.Add(_loc.Format("wiz.proj.shortcutFail", lnkErr));

        SwitchTo(folder, null, newConversation: true, d.Name);
        AddEntry(_loc["role.system"], _loc.Format("wiz.proj.created", d.Name, folder));
        foreach (var n in notes) AddEntry(_loc["role.system"], n);

        var msg = new StringBuilder(_loc.Format($"wiz.proj.msg.{Compat.Clamp(d.Plan, 0, 1)}", d.Name));
        if (d.Git == 2)
            msg.Append("\n\n").Append(_loc.Format("wiz.proj.msgGitHub", d.RepoName.Length > 0 ? d.RepoName : d.Name,
                d.RepoPublic ? _loc["wiz.proj.publicWord"] : _loc["wiz.proj.privateWord"]));
        if (copied.Count > 0) msg.Append("\n\n").Append(_loc.Format("wiz.proj.msgFiles", string.Join(", ", copied)));
        StartWith(d.CopyFiles ? Array.Empty<string>() : d.Files, msg.ToString());
        return true;
    }

    /// <summary>Runs a program without a window; returns an error text, or null.</summary>
    private static string? RunQuiet(string exe, string folder, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            psi.Arguments = Compat.JoinArguments(args);
            using var p = Process.Start(psi)!;
            string err = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit(30_000);
            return p.ExitCode == 0 ? null : (err.Trim().Length > 0 ? err.Trim() : $"exit {p.ExitCode}");
        }
        catch (Exception e) { return e.Message; }
    }

    /// <summary>A desktop shortcut that opens this window in the project folder.</summary>
    private string? CreateProjectShortcut(string folder, string name)
    {
        try
        {
            string exe = Compat.ProcessPath;
            string args = $"--project \"{folder}\"";
            if (!string.IsNullOrEmpty(_settings.RestartCommand)) args += $" --restart-command \"{_settings.RestartCommand}\"";
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{name} ({_settings.AppName}).lnk");
            var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell");
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(lnk);
            link.TargetPath = exe;
            link.Arguments = args;
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.Description = name;
            link.Save();
            return null;
        }
        catch (Exception e) { return e.Message; }
    }
}
