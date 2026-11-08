using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// File → Manage projects and conversations (author, 2026-11-08). Two boxes: a tree of projects
/// with their conversations, and the list of chats (they do not branch). Each item has a context
/// menu, and the same commands are the buttons below: Delete (hide it in the application; Open an
/// existing project/chat brings it back), Delete from disk too (into the Recycle Bin, after a
/// question), Rename (a project keeps its folder name on disk: only what the lists show changes),
/// Show memory (the CLAUDE.md the Settings button opens). Enter on a conversation opens it.
/// Everything is done at once, so there is only Close.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdOrgChats = 3910, IdOrgTree = 3911, IdOrgRename = 3912, IdOrgDelete = 3913, IdOrgDeleteDisk = 3914,
        IdOrgMemory = 3915, IdOrgNewProject = 3916, IdOrgNewChat = 3917, IdOrgRun = 3918;

    private const uint TVM_DELETEITEM = 0x1101, TVM_EXPAND = 0x1102, TVM_GETNEXTITEM = 0x110A, TVM_SELECTITEM = 0x110B,
        TVM_HITTEST = 0x1111, TVM_INSERTITEMW = 0x1132, TVM_GETITEMW = 0x113E, TVM_SETITEMW = 0x113F;
    private const uint TVIF_TEXT = 1, TVIF_PARAM = 4;
    private static readonly IntPtr TVI_ROOT = new(-65536), TVI_LAST = new(-65534);
    private const int NM_SETFOCUS = -7, LBN_SETFOCUS = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TVITEM
    {
        public uint mask; public IntPtr hItem; public uint state, stateMask; public string? pszText;
        public int cchTextMax, iImage, iSelectedImage, cChildren; public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TVINSERT { public IntPtr hParent, hInsertAfter; public TVITEM item; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TVHITTEST { public int x, y; public uint flags; public IntPtr hItem; }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)] private static extern IntPtr TvInsert(IntPtr h, uint m, IntPtr w, ref TVINSERT i);
    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)] private static extern IntPtr TvItem(IntPtr h, uint m, IntPtr w, ref TVITEM i);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr TvHit(IntPtr h, uint m, IntPtr w, ref TVHITTEST i);

    /// <summary>A tree entry: a project (Session null) or one of its conversations.</summary>
    private sealed record ManagerNode(string Folder, SessionCatalog.Session? Session);

    private string SessionLine(SessionCatalog.Session s) => $"{ConversationLabel(s)} — {s.When.ToString("g", _loc.Culture)}";

    private void ShowOrganizer()
    {
        if (WizardBlocked()) return;
        var f = BeginForm(NoAmp(_loc["menu.file.organizer"]));

        IntPtr lblTree = FormLabel(f, _loc["org.projects"]);
        IntPtr tree = FormChild(f, "SysTreeView32", "", 0x1 | 0x2 | 0x4 | 0x10 | 0x20 /* buttons, lines, at root, no drag, show selection */ | Win32.WS_TABSTOP, Win32.WS_EX_CLIENTEDGE, IdOrgTree);
        IntPtr lblChats = FormLabel(f, _loc["org.chats"]);
        IntPtr chatList = FormChild(f, "LISTBOX", "", 0x0001 | 0x0100 /* notify, no integral height */ | Win32.WS_VSCROLL | Win32.WS_TABSTOP, Win32.WS_EX_CLIENTEDGE, IdOrgChats);
        f.Extra.AddRange(new[] { lblTree, tree, lblChats, chatList });

        var buttons = new List<IntPtr>();
        foreach (var (id, key) in new[]
        {
            (IdOrgDelete, "org.delete"), (IdOrgDeleteDisk, "org.deleteDisk"), (IdOrgRename, "org.renameItem"), (IdOrgMemory, "org.memory"), (IdOrgRun, "org.run"),
            (IdOrgNewProject, "org.newProject"), (IdOrgNewChat, "org.newChat"), (FormCancel, "org.close"),
        })
            buttons.Add(FormChild(f, "BUTTON", NoAmp(_loc[key]), Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 0, id));
        f.Extra.AddRange(buttons);

        // ---- content
        var nodes = new List<ManagerNode>();
        IntPtr Insert(IntPtr parent, string text, ManagerNode node)
        {
            nodes.Add(node);
            var ins = new TVINSERT { hParent = parent, hInsertAfter = TVI_LAST, item = new TVITEM { mask = TVIF_TEXT | TVIF_PARAM, pszText = text, lParam = (IntPtr)(nodes.Count - 1) } };
            return TvInsert(tree, TVM_INSERTITEMW, IntPtr.Zero, ref ins);
        }
        IntPtr selectFirst = IntPtr.Zero;
        foreach (var p in ProjectFolders())
        {
            IntPtr item = Insert(TVI_ROOT, ProjectLabel(p), new ManagerNode(p, null));
            foreach (var s in OrderedSessions(p, 1000)) Insert(item, SessionLine(s), new ManagerNode(p, s));
            if (selectFirst == IntPtr.Zero || SameFolder(p, _project)) selectFirst = item;
        }
        if (selectFirst != IntPtr.Zero) Win32.SendMessageW(tree, TVM_SELECTITEM, (IntPtr)9 /* TVGN_CARET */, selectFirst);

        var chats = OrderedSessions(ChatFolderPath, 1000);
        foreach (var s in chats) Win32.SendMessageW(chatList, 0x0180 /* LB_ADDSTRING */, IntPtr.Zero, SessionLine(s));
        if (chats.Count > 0) Win32.SendMessageW(chatList, 0x0186 /* LB_SETCURSEL */, IntPtr.Zero, IntPtr.Zero);

        // ---- layout: the two boxes side by side, the buttons in one row below them
        f.CustomLayout = () =>
        {
            int pad = S(10), gap = S(6), lineH = Measure("Ag").cy, rowH = lineH + S(12), labelH = lineH + S(4);
            int[] widths = buttons.Select(b => Math.Max(S(70), Measure(Win32.GetText(b)).cx + S(24))).ToArray();
            int rowW = widths.Sum() + gap * (widths.Length - 1);
            int clientW = Math.Max(S(780), rowW + 2 * pad);
            int clientH = SizeForm(f, clientW, S(460));
            int boxW = (clientW - 3 * pad) / 2, boxY = pad + labelH, by = clientH - pad - rowH, boxH = by - gap - boxY;
            Move(lblTree, pad, pad, boxW, labelH);
            Move(tree, pad, boxY, boxW, boxH);
            Move(lblChats, pad * 2 + boxW, pad, boxW, labelH);
            Move(chatList, pad * 2 + boxW, boxY, boxW, boxH);
            int x = pad;
            for (int i = 0; i < buttons.Count; i++) { Move(buttons[i], x, by, widths[i], rowH); x += widths[i] + gap; }
        };

        // ---- which box the buttons work on: the one last in focus
        bool inChats = SameFolder(_project, ChatFolderPath);
        f.OnNotify = (ctl, code) => { if (ctl == tree && code == NM_SETFOCUS) inChats = false; };

        IntPtr TreeCaret() => Win32.SendMessageW(tree, TVM_GETNEXTITEM, (IntPtr)9 /* TVGN_CARET */, IntPtr.Zero);
        int ChatIndex() => (int)Win32.SendMessageW(chatList, 0x0188 /* LB_GETCURSEL */, IntPtr.Zero, IntPtr.Zero);
        ManagerNode? Target()
        {
            if (inChats) { int i = ChatIndex(); return i >= 0 && i < chats.Count ? new ManagerNode(ChatFolderPath, chats[i]) : null; }
            IntPtr h = TreeCaret();
            if (h == IntPtr.Zero) return null;
            var item = new TVITEM { mask = TVIF_PARAM, hItem = h };
            TvItem(tree, TVM_GETITEMW, IntPtr.Zero, ref item);
            int n = (int)item.lParam;
            return n >= 0 && n < nodes.Count ? nodes[n] : null;
        }

        void RemoveCurrent()
        {
            if (inChats)
            {
                int i = ChatIndex();
                chats.RemoveAt(i);
                Win32.SendMessageW(chatList, 0x0182 /* LB_DELETESTRING */, (IntPtr)i, IntPtr.Zero);
                if (chats.Count > 0) Win32.SendMessageW(chatList, 0x0186 /* LB_SETCURSEL */, (IntPtr)Math.Min(i, chats.Count - 1), IntPtr.Zero);
                Win32.SetFocus(chatList);
            }
            else
            {
                Win32.SendMessageW(tree, TVM_DELETEITEM, IntPtr.Zero, TreeCaret());
                Win32.SetFocus(tree);
            }
        }

        void SetCurrentText(string text)
        {
            if (inChats)
            {
                int i = ChatIndex();
                Win32.SendMessageW(chatList, 0x0182 /* LB_DELETESTRING */, (IntPtr)i, IntPtr.Zero);
                Win32.SendMessageW(chatList, 0x0181 /* LB_INSERTSTRING */, (IntPtr)i, text);
                Win32.SendMessageW(chatList, 0x0186 /* LB_SETCURSEL */, (IntPtr)i, IntPtr.Zero);
            }
            else
            {
                var item = new TVITEM { mask = TVIF_TEXT, hItem = TreeCaret(), pszText = text };
                TvItem(tree, TVM_SETITEMW, IntPtr.Zero, ref item);
            }
        }

        void Delete(bool fromDisk)
        {
            var t = Target();
            if (t == null) { Win32.MessageBeep(0); return; }
            if (t.Session is { } s)
            {
                if (SameFolder(t.Folder, _project) && s.Id == CurrentSessionId()) { Notice(f.Hwnd, _loc["org.isCurrent"]); return; }
                string label = ConversationLabel(s);
                if (fromDisk)
                {
                    if (!Confirm(f.Hwnd, _loc.Format("org.confirmConv", label))) return;
                    if (DeleteConversation(f.Hwnd, t.Folder, s.Id) is { } err) { Notice(f.Hwnd, _loc.Format("org.failed", err)); return; }
                    _speech.Speak(_loc.Format("org.deletedConv", label), true);
                }
                else
                {
                    if (!_state.HiddenConversations.Contains(s.Id)) _state.HiddenConversations.Add(s.Id);
                    foreach (var k in _state.Sessions.Where(kv => kv.Value == s.Id).Select(kv => kv.Key).ToList()) _state.Sessions.Remove(k);
                    _state.Save();
                    _speech.Speak(_loc.Format("org.hidden", label), true);
                }
                RemoveCurrent();
                return;
            }

            string folder = t.Folder, name = Path.GetFileName(folder);
            if (SameFolder(folder, _project)) { Notice(f.Hwnd, _loc["org.projectInUse"]); return; }
            string fullFolder = Path.GetFullPath(folder).TrimEnd('\\');
            if (!fromDisk)
            {
                _state.HiddenProjects.Add(fullFolder);
                _state.Save();
                _speech.Speak(_loc.Format("org.hidden", name), true);
                RemoveCurrent();
                return;
            }
            if (AppContext.BaseDirectory.StartsWith(fullFolder + "\\", StringComparison.OrdinalIgnoreCase)) { Notice(f.Hwnd, _loc["org.projectInUse"]); return; }
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{name} ({_settings.AppName}).lnk");
            var sessions = SessionCatalog.For(folder, 100000);
            var what = new StringBuilder(_loc.Format("org.confirmProject", name)).Append("\n\n");
            what.Append("- ").Append(_loc.Format("org.p.folder", folder)).Append('\n');
            what.Append("- ").Append(_loc.Format("org.p.conversations", sessions.Count, TranscriptReader.FolderFor(folder))).Append('\n');
            if (File.Exists(lnk)) what.Append("- ").Append(_loc.Format("org.p.shortcut", Path.GetFileName(lnk))).Append('\n');
            what.Append('\n').Append(_loc["org.p.githubNote"]);
            if (!Confirm(f.Hwnd, Crlf(what.ToString()))) return;
            if (Recycle(f.Hwnd, new[] { folder, TranscriptReader.FolderFor(folder), lnk }) is { } err2) { Notice(f.Hwnd, _loc.Format("org.failed", err2)); return; }
            foreach (var s2 in sessions) { _state.Conversations.Remove(s2.Id); _state.HiddenConversations.Remove(s2.Id); }
            _state.Sessions.Remove(fullFolder.ToUpperInvariant());
            _state.Drafts.Remove(fullFolder.ToUpperInvariant());
            _state.ExtraProjects.RemoveAll(e => SameFolder(e, fullFolder));
            _state.ProjectTitles.Remove(fullFolder);
            _state.Save();
            _speech.Speak(_loc.Format("org.deletedProject", name), true);
            RemoveCurrent();
        }

        void Rename()
        {
            var t = Target();
            if (t == null) { Win32.MessageBeep(0); return; }
            string current = t.Session is { } cs ? ConversationLabel(cs)
                : _state.ProjectTitles.TryGetValue(Path.GetFullPath(t.Folder).TrimEnd('\\'), out var pt) ? pt : Path.GetFileName(t.Folder);
            string? typed = null;
            var ask = BeginForm(_loc["org.renameTitle"]);
            IntPtr box = FEdit(ask, _loc["org.renameLabel"], current);
            FButton(ask, FormOk, _loc["wiz.confirm"], true);
            FButton(ask, FormCancel, _loc["wiz.cancel"]);
            ask.BeforeClose = r => { if (r == FormOk) typed = Win32.GetText(box).Trim(); };
            if (RunForm(ask, box) != FormOk || typed == null) return;

            if (t.Session is { } s)
            {
                if (!_state.Conversations.TryGetValue(s.Id, out var m)) _state.Conversations[s.Id] = m = new ConversationMeta();
                m.Title = typed.Length > 0 && typed != s.Label ? typed : null;
                _state.Save();
                SetCurrentText(SessionLine(s));
            }
            else
            {
                string key = Path.GetFullPath(t.Folder).TrimEnd('\\');
                if (typed.Length > 0 && typed != Path.GetFileName(t.Folder)) _state.ProjectTitles[key] = typed; else _state.ProjectTitles.Remove(key);
                _state.Save();
                SetCurrentText(ProjectLabel(t.Folder));
            }
            _speech.Speak(_loc["org.renamed"], true);
        }

        void Memory()
        {
            var t = Target();
            if (t == null) Win32.MessageBeep(0); else OpenInstructions(t.Folder);
        }

        Action? after = null;
        string? openFolder = null, openId = null, openLabel = null;

        void Command(int id)
        {
            switch (id)
            {
                case IdOrgDelete: Delete(false); break;
                case IdOrgDeleteDisk: Delete(true); break;
                case IdOrgRename: Rename(); break;
                case IdOrgMemory: Memory(); break;
                case IdOrgRun: { var r = Target(); if (r == null) Win32.MessageBeep(0); else RunProgram(f.Hwnd, r.Folder); break; }
                case IdOrgNewProject: after = NewProjectWizard; f.Result = FormCancel; f.Done = true; break;
                case IdOrgNewChat: after = NewChatWizard; f.Result = FormCancel; f.Done = true; break;
            }
        }

        f.OnCommand = (id, code) =>
        {
            if (id == IdOrgChats && code == LBN_SETFOCUS) { inChats = true; return false; }
            if (id is IdOrgDelete or IdOrgDeleteDisk or IdOrgRename or IdOrgMemory or IdOrgRun or IdOrgNewProject or IdOrgNewChat) { Command(id); return false; }
            if (id != FormOk) return false;

            // Enter: a conversation opens; on a project the branch opens or closes.
            var t = Target();
            if (t == null) { Win32.MessageBeep(0); return true; }
            if (t.Session is { } s) { openFolder = t.Folder; openId = s.Id; openLabel = ConversationLabel(s); return false; }
            Win32.SendMessageW(tree, TVM_EXPAND, (IntPtr)3 /* TVE_TOGGLE */, TreeCaret());
            return true;
        };

        f.OnContextMenu = (ctl, lParam) =>
        {
            if (ctl != tree && ctl != chatList) return;
            inChats = ctl == chatList;
            int sx = unchecked((short)((long)lParam & 0xFFFF)), sy = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
            bool mouse = sx != -1 || sy != -1;
            if (mouse)
            {
                var pt = new POINT { X = sx, Y = sy };
                Win32.ScreenToClient(ctl, ref pt);
                if (ctl == tree)
                {
                    var hit = new TVHITTEST { x = pt.X, y = pt.Y };
                    IntPtr h = TvHit(tree, TVM_HITTEST, IntPtr.Zero, ref hit);
                    if (h != IntPtr.Zero) Win32.SendMessageW(tree, TVM_SELECTITEM, (IntPtr)9, h);
                }
                else
                {
                    long r = (long)Win32.SendMessageW(chatList, 0x01A9 /* LB_ITEMFROMPOINT */, IntPtr.Zero, (IntPtr)((pt.Y << 16) | (pt.X & 0xFFFF)));
                    if (((r >> 16) & 0xFFFF) == 0) Win32.SendMessageW(chatList, 0x0186, (IntPtr)(r & 0xFFFF), IntPtr.Zero);
                }
            }
            if (Target() == null) return;
            if (!mouse) { Win32.GetWindowRect(ctl, out RECT rc); sx = rc.Left + S(24); sy = rc.Top + S(24); }

            IntPtr menu = Win32.CreatePopupMenu();
            foreach (var (id, key) in new[] { (IdOrgDelete, "org.delete"), (IdOrgDeleteDisk, "org.deleteDisk"), (IdOrgRename, "org.renameItem"), (IdOrgMemory, "org.memory"), (IdOrgRun, "org.run") })
                Win32.AppendMenuW(menu, Win32.MF_STRING, (IntPtr)id, _loc[key]);
            Win32.SetForegroundWindow(f.Hwnd);
            int chosen = Win32.TrackPopupMenuEx(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON, sx, sy, f.Hwnd, IntPtr.Zero);
            Win32.DestroyMenu(menu);
            if (chosen != 0) Command(chosen);
        };

        int result = RunForm(f, inChats ? chatList : tree);
        if (result == FormOk && openFolder != null && openId != null)
            SwitchTo(openFolder, openId, newConversation: false, openLabel);
        after?.Invoke();
    }
}
