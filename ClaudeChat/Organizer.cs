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

    /// <summary>Newest <paramref name="max"/> conversations of a folder. In Chat newest first; in a
    /// project oldest first, so the number grows with time (author, 2026-11-07).</summary>
    private List<SessionCatalog.Session> OrderedSessions(string folder, int max)
    {
        var list = (Directory.Exists(folder) ? SessionCatalog.For(folder, max) : new List<SessionCatalog.Session>())
            .Where(s => !_state.HiddenConversations.Contains(s.Id)).ToList();
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

}
