using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// File → Conversations and projects… (author, 2026-11-07): open, rename and delete conversations,
/// delete projects. Deleting always goes to the Recycle Bin and always asks first (No is the
/// default). A project is deleted with its conversations and memory; its folder and files only
/// when "Also delete the project folder with its files" is checked — otherwise the folder stays
/// on disk and is just left out of the Code list. The conversation or project the window is in,
/// and the folder the window runs from, cannot be deleted.
/// Also Session → Delete this conversation…, which starts a new one in the same folder.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmOrganizer = 207, IdmDeleteCurrent = 242;
    private const int IdOrgFolder = 3910, IdOrgList = 3911, IdOrgRename = 3912, IdOrgDelete = 3913, IdOrgDeleteProject = 3914;

    /// <summary>Newest <paramref name="max"/> conversations of a folder. In Chat newest first; in a
    /// project oldest first, so the number grows with time (author, 2026-11-07).</summary>
    private List<SessionCatalog.Session> OrderedSessions(string folder, int max)
    {
        var list = Directory.Exists(folder) ? SessionCatalog.For(folder, max) : new List<SessionCatalog.Session>();
        return SameFolder(folder, ChatFolderPath) ? list : list.OrderBy(s => s.When).ToList();
    }

    private string ConversationLabel(SessionCatalog.Session s) =>
        _state.Conversations.TryGetValue(s.Id, out var m) && !string.IsNullOrEmpty(m.Title) ? m.Title! : s.Label;

    private bool IsHidden(string folder) => _state.HiddenProjects.Contains(Path.GetFullPath(folder).TrimEnd('\\'), StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ Recycle Bin

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCT op);

    /// <summary>Moves files and folders to the Recycle Bin; returns an error text, or null.</summary>
    private static string? Recycle(IntPtr owner, IEnumerable<string> paths)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (existing.Count == 0) return null;
        var op = new SHFILEOPSTRUCT
        {
            hwnd = owner,
            wFunc = 3, // FO_DELETE
            pFrom = string.Join("\0", existing) + "\0\0",
            fFlags = 0x0040 | 0x0010 | 0x0004 | 0x0400, // ALLOWUNDO | NOCONFIRMATION | SILENT | NOERRORUI
        };
        int r = SHFileOperationW(ref op);
        if (r != 0) return $"0x{r:X}";
        if (op.fAnyOperationsAborted != 0) return "aborted";
        return existing.Any(p => File.Exists(p) || Directory.Exists(p)) ? "still there" : null;
    }

    private static IEnumerable<string> TranscriptPaths(string folder, string sessionId)
    {
        string dir = TranscriptReader.FolderFor(folder);
        yield return Path.Combine(dir, sessionId + ".jsonl");
        yield return Path.Combine(dir, sessionId); // subagents and tool results, when there are any
    }

    private bool Confirm(IntPtr owner, string text) =>
        Win32.MessageBoxW(owner, text, _settings.AppName, 0x04 /* YESNO */ | 0x30 /* WARNING */ | 0x100 /* DEFBUTTON2 */) == 6;

    private void Notice(IntPtr owner, string text) =>
        Win32.MessageBoxW(owner, text, _settings.AppName, Win32.MB_OK | Win32.MB_ICONINFORMATION);

    /// <summary>Deletes one conversation (to the Recycle Bin) and what the window remembers of it.</summary>
    private string? DeleteConversation(IntPtr owner, string folder, string sessionId)
    {
        string? err = Recycle(owner, TranscriptPaths(folder, sessionId));
        if (err != null) return err;
        _state.Conversations.Remove(sessionId);
        foreach (var k in _state.Sessions.Where(kv => kv.Value == sessionId).Select(kv => kv.Key).ToList()) _state.Sessions.Remove(k);
        _state.Save();
        return null;
    }

    // ------------------------------------------------------------------ Session → Delete this conversation

    private void DeleteCurrentConversation()
    {
        if (WizardBlocked()) return;
        string? id = CurrentSessionId();
        if (id == null) { Notice(_hwnd, _loc["org.noCurrent"]); return; }
        var s = SessionCatalog.For(_project, 1000).FirstOrDefault(x => x.Id == id);
        string label = s != null ? ConversationLabel(s) : id;
        if (!Confirm(_hwnd, _loc.Format("org.confirmConv", label))) return;
        _session?.Kill();
        _session = null;
        if (DeleteConversation(_hwnd, _project, id) is { } err) { Notice(_hwnd, _loc.Format("org.failed", err)); return; }
        SwitchTo(_project, null, newConversation: true, null);
        AddEntry(_loc["role.system"], _loc.Format("org.deletedConv", label));
    }

    // ------------------------------------------------------------------ the dialog

    private void ShowOrganizer()
    {
        if (WizardBlocked()) return;
        var folders = new List<string> { ChatFolderPath };
        folders.AddRange(ProjectFolders());
        int folderIndex = Math.Max(0, folders.FindIndex(f => SameFolder(f, _project)));
        List<SessionCatalog.Session> sessions = new();
        string? openFolder = null, openId = null, openLabel = null;

        var f = BeginForm(_loc["org.title"]);
        IntPtr folderCombo = FCombo(f, _loc["org.folder"], folders.Select((p, i) => i == 0 ? _loc["menu.file.chat"].Replace("&", "") : ProjectLabel(p)), folderIndex, IdOrgFolder);
        IntPtr list = FList(f, _loc["org.conversations"], Array.Empty<string>(), 10, IdOrgList);
        IntPtr title = FEdit(f, _loc["org.newTitle"], "");
        FButtonRow(f, (IdOrgRename, _loc["org.rename"]), (IdOrgDelete, _loc["org.deleteConv"]));
        IntPtr withFiles = FCheck(f, _loc["org.withFiles"], false);
        FButtonRow(f, (IdOrgDeleteProject, _loc["org.deleteProject"]));
        FBig(f, _loc["org.info"], "", true);
        FButton(f, FormOk, _loc["org.open"], true);
        FButton(f, FormCancel, _loc["org.close"]);

        string Folder() => folders[Math.Clamp(Selected(folderCombo), 0, folders.Count - 1)];
        int Sel() => (int)Win32.SendMessageW(list, 0x0188 /* LB_GETCURSEL */, IntPtr.Zero, IntPtr.Zero);
        SessionCatalog.Session? Current() { int i = Sel(); return i >= 0 && i < sessions.Count ? sessions[i] : null; }

        void ShowInfo()
        {
            string folder = Folder();
            var sb = new StringBuilder();
            var s = Current();
            if (s != null)
            {
                var file = new FileInfo(Path.Combine(TranscriptReader.FolderFor(folder), s.Id + ".jsonl"));
                sb.Append(_loc["org.i.title"]).Append(": ").Append(ConversationLabel(s)).Append('\n');
                sb.Append(_loc["org.i.when"]).Append(": ").Append(s.When.ToString("g", _loc.Culture)).Append('\n');
                if (file.Exists) sb.Append(_loc["org.i.size"]).Append(": ").Append((file.Length + 1023) / 1024).Append(" KB\n");
                if (_state.Conversations.TryGetValue(s.Id, out var m) && !string.IsNullOrEmpty(m.Instructions))
                    sb.Append(_loc["org.i.instructions"]).Append(": ").Append(m.Instructions).Append('\n');
                if (SameFolder(folder, _project) && s.Id == CurrentSessionId()) sb.Append(_loc["org.i.current"]).Append('\n');
                sb.Append('\n');
            }
            sb.Append(_loc["org.i.folder"]).Append(": ").Append(folder).Append('\n');
            sb.Append(_loc["org.i.count"]).Append(": ").Append(sessions.Count).Append('\n');
            if (!SameFolder(folder, ChatFolderPath))
                sb.Append("Git: ").Append(Directory.Exists(Path.Combine(folder, ".git")) ? _loc["wiz.yes"] : _loc["wiz.no"]).Append('\n');
            Win32.SetWindowTextW(f.Big, ToEdit(sb.ToString()));
            Win32.SetWindowTextW(title, s != null ? ConversationLabel(s) : "");
        }

        void FillList(int select)
        {
            string folder = Folder();
            sessions = OrderedSessions(folder, 1000);
            Win32.SendMessageW(list, 0x0184 /* LB_RESETCONTENT */, IntPtr.Zero, IntPtr.Zero);
            for (int i = 0; i < sessions.Count; i++)
                Win32.SendMessageW(list, 0x0180 /* LB_ADDSTRING */, IntPtr.Zero, $"{i + 1} {ConversationLabel(sessions[i])} — {sessions[i].When.ToString("g", _loc.Culture)}");
            if (sessions.Count > 0) Win32.SendMessageW(list, 0x0186 /* LB_SETCURSEL */, (IntPtr)Math.Clamp(select, 0, sessions.Count - 1), IntPtr.Zero);
            bool isChat = SameFolder(folder, ChatFolderPath);
            Win32.EnableWindow(withFiles, !isChat);
            ShowInfo();
        }
        FillList(0);

        f.OnCommand = (id, code) =>
        {
            switch (id)
            {
                case IdOrgFolder when code == Win32.CBN_SELCHANGE: FillList(0); break;
                case IdOrgList when code == 1 /* LBN_SELCHANGE */: ShowInfo(); break;
                case IdOrgRename:
                {
                    var s = Current();
                    if (s == null) { Win32.MessageBeep(0); break; }
                    string t = Win32.GetText(title).Trim();
                    if (!_state.Conversations.TryGetValue(s.Id, out var m)) _state.Conversations[s.Id] = m = new ConversationMeta();
                    m.Title = t.Length > 0 && t != s.Label ? t : null;
                    _state.Save();
                    FillList(Sel());
                    _speech.Speak(_loc["org.renamed"], true);
                    break;
                }
                case IdOrgDelete:
                {
                    var s = Current();
                    string folder = Folder();
                    if (s == null) { Win32.MessageBeep(0); break; }
                    if (SameFolder(folder, _project) && s.Id == CurrentSessionId()) { Notice(f.Hwnd, _loc["org.isCurrent"]); break; }
                    if (!Confirm(f.Hwnd, _loc.Format("org.confirmConv", ConversationLabel(s)))) break;
                    if (DeleteConversation(f.Hwnd, folder, s.Id) is { } err) Notice(f.Hwnd, _loc.Format("org.failed", err));
                    else _speech.Speak(_loc.Format("org.deletedConv", ConversationLabel(s)), true);
                    int keep = Sel();
                    FillList(keep);
                    Win32.SetFocus(list);
                    break;
                }
                case IdOrgDeleteProject:
                {
                    string folder = Folder();
                    string name = Path.GetFileName(folder);
                    if (SameFolder(folder, ChatFolderPath)) { Notice(f.Hwnd, _loc["org.notChat"]); break; }
                    string exeDir = AppContext.BaseDirectory;
                    if (SameFolder(folder, _project) || exeDir.StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    { Notice(f.Hwnd, _loc["org.projectInUse"]); break; }
                    bool files = Checked(withFiles);
                    string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{name} ({_settings.AppName}).lnk");
                    var what = new StringBuilder(_loc.Format("org.confirmProject", name)).Append("\n\n");
                    what.Append("- ").Append(_loc.Format("org.p.conversations", sessions.Count, TranscriptReader.FolderFor(folder))).Append('\n');
                    if (files) what.Append("- ").Append(_loc.Format("org.p.folder", folder)).Append('\n');
                    if (File.Exists(lnk)) what.Append("- ").Append(_loc.Format("org.p.shortcut", Path.GetFileName(lnk))).Append('\n');
                    what.Append('\n').Append(files ? _loc["org.p.githubNote"] : _loc.Format("org.p.staysOnDisk", folder));
                    if (!Confirm(f.Hwnd, what.ToString())) break;

                    var paths = new List<string> { TranscriptReader.FolderFor(folder), lnk };
                    if (files) paths.Add(folder);
                    if (Recycle(f.Hwnd, paths) is { } err) { Notice(f.Hwnd, _loc.Format("org.failed", err)); break; }
                    foreach (var s in sessions) _state.Conversations.Remove(s.Id);
                    _state.Sessions.Remove(folder.TrimEnd('\\').ToUpperInvariant());
                    _state.Drafts.Remove(folder.TrimEnd('\\').ToUpperInvariant());
                    if (!files) _state.HiddenProjects.Add(Path.GetFullPath(folder).TrimEnd('\\'));
                    _state.Save();
                    _speech.Speak(_loc.Format("org.deletedProject", name), true);

                    int idx = Selected(folderCombo);
                    folders.RemoveAt(idx);
                    Win32.SendMessageW(folderCombo, 0x0144 /* CB_DELETESTRING */, (IntPtr)idx, IntPtr.Zero);
                    Win32.SendMessageW(folderCombo, Win32.CB_SETCURSEL, IntPtr.Zero, IntPtr.Zero);
                    Check(withFiles, false);
                    FillList(0);
                    Win32.SetFocus(folderCombo);
                    break;
                }
                case FormOk:
                {
                    var s = Current();
                    if (s == null) { Win32.MessageBeep(0); return true; }
                    openFolder = Folder(); openId = s.Id; openLabel = ConversationLabel(s);
                    break;
                }
            }
            return false;
        };

        int result = RunForm(f, list);
        if (result == FormOk && openFolder != null && openId != null)
            SwitchTo(openFolder, openId, newConversation: false, openLabel);
    }
}
