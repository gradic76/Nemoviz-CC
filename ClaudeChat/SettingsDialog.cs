using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// File → Settings… (author, 2026-11-08): a modal dialog with two tabs, General and Sounds and
/// devices. Standard controls only; Tab walks the visible page, Ctrl+Tab / Ctrl+Shift+Tab change
/// the page, Enter is OK, Escape is Cancel. Apply keeps the dialog open.
/// The tab strip is only as tall as its headers and the pages sit below it, so nothing overlaps
/// and the Tab order stays the creation order.
/// </summary>
internal sealed partial class MainWindow
{
    private const string SettingsClass = "ClaudeChat.Settings";
    private static readonly WndProcDelegate s_settingsProc = SettingsProcStatic;
    private static bool s_settingsClassRegistered;

    private const int IdOk = 1, IdCancel = 2, IdApply = 2003, IdTab = 2004;
    private const int IdChangeRoot = 2010, IdListenSend = 2011, IdListenBell = 2012, IdListenTyping = 2013, IdEditProject = 2014;
    private const int PageCount = 3;

    private IntPtr _dlg, _tab;
    private readonly List<IntPtr> _pageGeneral = new(), _pageSounds = new(), _pageClaude = new();
    private IntPtr _edPersonal, _edChat, _btnEditProject;
    private string? _diskPersonal, _diskChat; // the files as they were when the dialog opened; null = missing
    private IntPtr _cbLang, _cbTheme, _cbFont, _chkSpeakSent, _chkSpeakReplies, _chkResume, _chkUpdates, _cbHandoff, _edRoot;
    private IntPtr _chkSend, _chkBell, _chkTyping, _cbDevice, _chkKeepAwake, _cbVolSend, _cbVolBell, _cbVolTyping;
    private string? _dlgRoot;
    private List<string> _dlgDevices = new();
    private bool _dlgClose;
    private IntPtr _dlgFocus;

    // Named like the old browsers (author, 2026-11-07): Smallest … Largest, Normal in the middle.
    private static readonly int[] FontChoices = { 80, 90, 100, 125, 150 };
    private static readonly int[] HandoffChoices = { 0, 80, 85, 90, 95 };
    private static readonly string[] ThemeChoices = { "system", "light", "dark" };

    private void ShowSettings()
    {
        if (_dlg != IntPtr.Zero) return;
        if (!s_settingsClassRegistered) { RegisterClass(SettingsClass, s_settingsProc); s_settingsClassRegistered = true; }

        IntPtr focusBefore = Win32.GetFocus();
        int w = S(470), h = S(470);
        Win32.GetWindowRect(_hwnd, out RECT owner);
        int x = owner.Left + Math.Max(0, (owner.Width - w) / 2), y = owner.Top + S(60);
        _dlg = Win32.CreateWindowExW(Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_CONTROLPARENT, SettingsClass, _loc["set.title"],
            Win32.WS_POPUP | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_CLIPCHILDREN, x, y, w, h, _hwnd, IntPtr.Zero, _hInst, IntPtr.Zero);
        BuildSettingsControls();
        LoadSettingsIntoDialog();
        ShowPage(0);
        _theme.ApplyToTopLevel(_dlg);

        Win32.EnableWindow(_hwnd, false);
        Win32.ShowWindow(_dlg, Win32.SW_SHOWNORMAL);
        Win32.SetFocus(_tab);

        // Modal loop: runs until OK / Cancel / close.
        _dlgClose = false;
        while (!_dlgClose)
        {
            if (Win32.GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) <= 0)
            {
                Win32.PostQuitMessage(0); // hand WM_QUIT back to the main loop
                break;
            }
            if (msg.message == Win32.WM_KEYDOWN && (int)msg.wParam == Win32.VK_TAB && Win32.KeyDown(Win32.VK_CONTROL))
            {
                int page = (int)Win32.SendMessageW(_tab, Win32.TCM_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
                page = (page + (Win32.KeyDown(Win32.VK_SHIFT) ? PageCount - 1 : 1)) % PageCount;
                Win32.SendMessageW(_tab, Win32.TCM_SETCURSEL, (IntPtr)page, IntPtr.Zero);
                ShowPage(page);
                Win32.SetFocus(_tab);
                continue;
            }
            // A multi-line EDIT wants every key, Tab too; in this dialog Tab moves on (Enter
            // still makes a new line there).
            if (msg.message == Win32.WM_KEYDOWN && (int)msg.wParam == Win32.VK_TAB
                && (msg.hwnd == _edPersonal || msg.hwnd == _edChat))
            {
                IntPtr next = Win32.GetNextDlgTabItem(_dlg, msg.hwnd, Win32.KeyDown(Win32.VK_SHIFT));
                if (next != IntPtr.Zero) Win32.SetFocus(next);
                continue;
            }
            if (Win32.IsDialogMessageW(_dlg, ref msg)) continue;
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }

        Win32.EnableWindow(_hwnd, true);
        Win32.DestroyWindow(_dlg);
        _dlg = _tab = IntPtr.Zero;
        _pageGeneral.Clear(); _pageSounds.Clear(); _pageClaude.Clear();
        _edPersonal = _edChat = _btnEditProject = IntPtr.Zero;
        _dlgFocus = IntPtr.Zero;
        Win32.SetForegroundWindow(_hwnd);
        Win32.SetFocus(focusBefore != IntPtr.Zero && Win32.IsWindow(focusBefore) ? focusBefore : _input);
    }

    private IntPtr DlgChild(string cls, string text, uint style, uint ex, int id, List<IntPtr>? page)
    {
        IntPtr h = Win32.CreateWindowExW(ex, cls, text, Win32.WS_CHILD | style, 0, 0, 0, 0, _dlg, (IntPtr)id, _hInst, IntPtr.Zero);
        Win32.SendMessageW(h, Win32.WM_SETFONT, _font, (IntPtr)1);
        page?.Add(h);
        return h;
    }

    private static string NoAmp(string s) => s.Replace("&", "");

    /// <summary>Creation order = Tab order: tab strip, General, Sounds and devices, OK, Cancel, Apply.</summary>
    private void BuildSettingsControls()
    {
        const uint tabStop = Win32.WS_TABSTOP;
        const uint label = Win32.SS_LEFT | Win32.SS_NOPREFIX;
        const uint combo = Win32.CBS_DROPDOWNLIST | Win32.WS_VSCROLL | tabStop;
        const uint check = Win32.BS_AUTOCHECKBOX | tabStop;
        const uint button = Win32.BS_PUSHBUTTON | tabStop;

        _tab = DlgChild("SysTabControl32", "", Win32.WS_VISIBLE | tabStop, 0, IdTab, null);
        foreach (var key in new[] { "set.tab.general", "set.tab.sounds", "set.tab.claude" })
        {
            var item = new Win32.TCITEM { mask = 1 /* TCIF_TEXT */, pszText = _loc[key] };
            int count = (int)Win32.SendMessageW(_tab, 0x1304 /* TCM_GETITEMCOUNT */, IntPtr.Zero, IntPtr.Zero);
            Win32.SendMessageW(_tab, Win32.TCM_INSERTITEMW, (IntPtr)count, ref item);
        }

        var g = _pageGeneral;
        DlgChild("STATIC", _loc["set.language"], label, 0, 0, g);
        _cbLang = DlgChild("COMBOBOX", "", combo, 0, 0, g);
        DlgChild("STATIC", _loc["set.theme"], label, 0, 0, g);
        _cbTheme = DlgChild("COMBOBOX", "", combo, 0, 0, g);
        DlgChild("STATIC", _loc["set.font"], label, 0, 0, g);
        _cbFont = DlgChild("COMBOBOX", "", combo, 0, 0, g);
        _chkResume = DlgChild("BUTTON", NoAmp(_loc["menu.session.resume"]), check, 0, 0, g);
        _chkUpdates = DlgChild("BUTTON", _loc["set.updates"], check, 0, 0, g);
        DlgChild("STATIC", _loc["set.handoff"], label, 0, 0, g);
        _cbHandoff = DlgChild("COMBOBOX", "", combo, 0, 0, g);
        DlgChild("STATIC", _loc["set.projects"], label, 0, 0, g);
        _edRoot = DlgChild("EDIT", "", Win32.ES_READONLY | Win32.ES_AUTOHSCROLL | tabStop, Win32.WS_EX_CLIENTEDGE, 0, g);
        DlgChild("BUTTON", _loc["set.change"], button, 0, IdChangeRoot, g);

        var s = _pageSounds;
        _chkSend = DlgChild("BUTTON", _loc["set.soundSend"], check, 0, 0, s);
        DlgChild("BUTTON", _loc["set.listenSend"], button, 0, IdListenSend, s);
        DlgChild("STATIC", _loc["set.volumeSend"], label, 0, 0, s);
        _cbVolSend = DlgChild("COMBOBOX", "", combo, 0, 0, s);
        _chkBell = DlgChild("BUTTON", _loc["set.soundBell"], check, 0, 0, s);
        DlgChild("BUTTON", _loc["set.listenBell"], button, 0, IdListenBell, s);
        DlgChild("STATIC", _loc["set.volumeBell"], label, 0, 0, s);
        _cbVolBell = DlgChild("COMBOBOX", "", combo, 0, 0, s);
        _chkTyping = DlgChild("BUTTON", _loc["set.soundTyping"], check, 0, 0, s);
        DlgChild("BUTTON", _loc["set.listenTyping"], button, 0, IdListenTyping, s);
        DlgChild("STATIC", _loc["set.volumeTyping"], label, 0, 0, s);
        _cbVolTyping = DlgChild("COMBOBOX", "", combo, 0, 0, s);
        _chkSpeakSent = DlgChild("BUTTON", NoAmp(_loc["menu.speech.sent"]), check, 0, 0, s);
        _chkSpeakReplies = DlgChild("BUTTON", NoAmp(_loc["menu.speech.replies"]), check, 0, 0, s);
        DlgChild("STATIC", _loc["set.device"], label, 0, 0, s);
        _cbDevice = DlgChild("COMBOBOX", "", combo, 0, 0, s);
        _chkKeepAwake = DlgChild("BUTTON", _loc["set.keepAwake"], check, 0, 0, s);

        var c = _pageClaude;
        const uint multi = Win32.ES_MULTILINE | Win32.ES_WANTRETURN | Win32.ES_AUTOVSCROLL | Win32.WS_VSCROLL | tabStop;
        DlgChild("STATIC", _loc["set.instrPersonal"], label, 0, 0, c);
        _edPersonal = DlgChild("EDIT", "", multi, Win32.WS_EX_CLIENTEDGE, 0, c);
        DlgChild("STATIC", _loc["set.instrChat"], label, 0, 0, c);
        _edChat = DlgChild("EDIT", "", multi, Win32.WS_EX_CLIENTEDGE, 0, c);
        _btnEditProject = DlgChild("BUTTON", _loc.Format("set.editProject", Path.GetFileName(_project.TrimEnd('\\', '/'))), button, 0, IdEditProject, c);
        foreach (var e in new[] { _edPersonal, _edChat }) Win32.SendMessageW(e, Win32.EM_SETLIMITTEXT, IntPtr.Zero, IntPtr.Zero);
        // In the Chat folder the project's CLAUDE.md is the chat instructions above.
        if (SameFolder(_project, ChatFolderPath)) Win32.EnableWindow(_btnEditProject, false);

        DlgChild("BUTTON", _loc["set.ok"], Win32.WS_VISIBLE | Win32.BS_DEFPUSHBUTTON | tabStop, 0, IdOk, null);
        DlgChild("BUTTON", _loc["set.cancel"], Win32.WS_VISIBLE | button, 0, IdCancel, null);
        DlgChild("BUTTON", _loc["set.apply"], Win32.WS_VISIBLE | button, 0, IdApply, null);

        LayoutSettings();
        foreach (var ctl in _pageGeneral.Concat(_pageSounds).Concat(_pageClaude))
            ThemeDialogControl(ctl);
    }

    private void ThemeDialogControl(IntPtr c)
    {
        var cls = new System.Text.StringBuilder(32);
        GetClassNameW(c, cls, 32);
        switch (cls.ToString())
        {
            case "ComboBox": _theme.ApplyToControl(c, Theme.ControlRole.Combo); break;
            case "Edit": _theme.ApplyToControl(c, Theme.ControlRole.Field); break;
            case "ListBox": _theme.ApplyToControl(c, Theme.ControlRole.Field); break;
            case "Button":
                long style = (long)GetWindowLongPtrW(c, -16);
                _theme.ApplyToControl(c, (style & 0xF) == Win32.BS_AUTOCHECKBOX ? Theme.ControlRole.CheckBox : Theme.ControlRole.PushButton);
                break;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtrW(IntPtr h, int index);

    private void LayoutSettings()
    {
        Win32.GetClientRect(_dlg, out RECT rc);
        int pad = S(10), gap = S(6), lineH = Measure("Ag").cy, rowH = lineH + S(12), labelH = lineH + S(2);
        int innerW = rc.Width - 2 * pad;
        Move(_tab, pad, pad, innerW, lineH + S(14));
        int top = pad + lineH + S(14) + gap * 2;

        // A page is a column of rows; a label row is followed by its control.
        int Column(List<IntPtr> page)
        {
            int y = top;
            for (int i = 0; i < page.Count; i++)
            {
                IntPtr c = page[i];
                var cls = new System.Text.StringBuilder(32);
                GetClassNameW(c, cls, 32);
                string k = cls.ToString();
                int id = (int)GetDlgCtrlID(c);
                if (k == "Static") { Move(c, pad, y, innerW, labelH); y += labelH; continue; }
                bool listen = id is IdListenSend or IdListenBell or IdListenTyping;
                bool change = id == IdChangeRoot;
                if (listen || change) continue; // placed next to the control before them
                int btnW = Math.Max(S(110), Measure(_loc["set.listenBell"]).cx + S(24));
                bool hasButton = i + 1 < page.Count && (int)GetDlgCtrlID(page[i + 1]) is IdListenSend or IdListenBell or IdListenTyping or IdChangeRoot;
                int w = hasButton ? innerW - btnW - gap : innerW;
                bool multiLine = k == "Edit" && ((long)GetWindowLongPtrW(c, -16) & Win32.ES_MULTILINE) != 0;
                int h = multiLine ? lineH * 9 + S(8) : rowH;
                Move(c, pad, y, w, k == "ComboBox" ? rowH + S(200) : h);
                if (hasButton) Move(page[i + 1], pad + w + gap, y, btnW, rowH);
                y += h + gap;
            }
            return y;
        }
        int bottom = Math.Max(Column(_pageGeneral), Math.Max(Column(_pageSounds), Column(_pageClaude)));

        // The dialog is as tall as its longer page plus the button row (font size and DPI vary).
        int need = bottom + gap + rowH + pad;
        if (need != rc.Height)
        {
            Win32.GetWindowRect(_dlg, out RECT wr);
            Win32.SetWindowPos(_dlg, IntPtr.Zero, 0, 0, wr.Width, wr.Height + need - rc.Height, 0x0002 | 0x0004 | 0x0010 /* NOMOVE|NOZORDER|NOACTIVATE */);
            Win32.GetClientRect(_dlg, out rc);
        }

        int bw = Math.Max(S(100), Measure(_loc["set.cancel"]).cx + S(28));
        int by = rc.Height - pad - rowH;
        Move(GetDlgItem(_dlg, IdApply), rc.Width - pad - bw, by, bw, rowH);
        Move(GetDlgItem(_dlg, IdCancel), rc.Width - pad - 2 * bw - gap, by, bw, rowH);
        Move(GetDlgItem(_dlg, IdOk), rc.Width - pad - 3 * bw - 2 * gap, by, bw, rowH);
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr dlg, int id);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr h);

    private void ShowPage(int page)
    {
        foreach (var c in _pageGeneral) Win32.ShowWindow(c, page == 0 ? Win32.SW_SHOW : 0);
        foreach (var c in _pageSounds) Win32.ShowWindow(c, page == 1 ? Win32.SW_SHOW : 0);
        foreach (var c in _pageClaude) Win32.ShowWindow(c, page == 2 ? Win32.SW_SHOW : 0);
    }

    // ------------------------------------------------------------------ values

    private static void Check(IntPtr box, bool on) => Win32.SendMessageW(box, Win32.BM_SETCHECK, (IntPtr)(on ? 1 : 0), IntPtr.Zero);
    private static bool Checked(IntPtr box) => (long)Win32.SendMessageW(box, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == Win32.BST_CHECKED;
    private static int Selected(IntPtr combo) => (int)Win32.SendMessageW(combo, Win32.CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero);

    private static void Fill(IntPtr combo, IEnumerable<string> items, int selected)
    {
        Win32.SendMessageW(combo, Win32.CB_RESETCONTENT, IntPtr.Zero, IntPtr.Zero);
        foreach (var t in items) Win32.SendMessageW(combo, Win32.CB_ADDSTRING, IntPtr.Zero, t);
        Win32.SendMessageW(combo, Win32.CB_SETCURSEL, (IntPtr)Math.Max(0, selected), IntPtr.Zero);
    }

    // Volumes 10 %, 20 % … 100 %.
    private static void FillVolume(IntPtr combo, int percent) =>
        Fill(combo, Enumerable.Range(1, 10).Select(v => $"{v * 10} %"), Math.Clamp((int)Math.Round(percent / 10.0) - 1, 0, 9));
    private static int VolumeOf(IntPtr combo) => (Math.Clamp(Selected(combo), 0, 9) + 1) * 10;

    private void LoadSettingsIntoDialog()
    {
        var langs = Localization.Available();
        int li = _settings.Language.Equals("auto", StringComparison.OrdinalIgnoreCase) ? 0
            : 1 + langs.ToList().FindIndex(l => l.Code.Equals(_settings.Language, StringComparison.OrdinalIgnoreCase));
        Fill(_cbLang, new[] { _loc["set.language.auto"] }.Concat(langs.Select(l => l.Name)), li);
        Fill(_cbTheme, new[] { "menu.view.theme.system", "menu.view.theme.light", "menu.view.theme.dark" }.Select(k => NoAmp(_loc[k])),
            Array.IndexOf(ThemeChoices, _settings.Theme.ToLowerInvariant()));
        int font = (int)Math.Round(_settings.FontScale * 100);
        Fill(_cbFont, Enumerable.Range(1, FontChoices.Length).Select(i => _loc[$"set.font.{i}"]), Array.IndexOf(FontChoices, FontChoices.OrderBy(f => Math.Abs(f - font)).First()));
        Check(_chkSpeakSent, _settings.SpeakSent);
        Check(_chkSpeakReplies, _settings.SpeakReplies);
        Check(_chkResume, _settings.ResumeAfterLimit);
        Check(_chkUpdates, _settings.CheckUpdates);
        Fill(_cbHandoff, HandoffChoices.Select(p => p == 0 ? _loc["set.handoff.off"] : $"{p} %"),
            Array.IndexOf(HandoffChoices, HandoffChoices.OrderBy(p => Math.Abs(p - _settings.ContextHandoffPercent)).First()));
        _dlgRoot = ProjectsRoot;
        Win32.SetWindowTextW(_edRoot, _dlgRoot);

        Check(_chkSend, _settings.SoundSend);
        Check(_chkBell, _settings.SoundBell);
        Check(_chkTyping, _settings.SoundTyping);
        Check(_chkKeepAwake, _settings.KeepAudioDeviceAwake);
        FillVolume(_cbVolSend, _settings.SoundSendVolume);
        FillVolume(_cbVolBell, _settings.SoundBellVolume);
        FillVolume(_cbVolTyping, _settings.SoundTypingVolume);
        _dlgDevices = AudioEngine.Devices().Select(d => d.Name).ToList();
        int di = string.IsNullOrEmpty(_settings.AudioDevice) ? 0 : 1 + _dlgDevices.IndexOf(_settings.AudioDevice!);
        Fill(_cbDevice, new[] { _loc["set.device.default"] }.Concat(_dlgDevices), Math.Max(0, di));

        _diskPersonal = ReadInstructions(PersonalInstructionsPath);
        _diskChat = ReadInstructions(ChatInstructionsPath);
        Win32.SetWindowTextW(_edPersonal, _diskPersonal ?? ToEdit(_loc["tpl.personal"]));
        Win32.SetWindowTextW(_edChat, _diskChat ?? ToEdit(_loc["tpl.chat"]));
    }

    /// <summary>Writes every value from the dialog into the settings and applies it at once.</summary>
    private void ApplySettingsFromDialog()
    {
        var langs = Localization.Available();
        int li = Selected(_cbLang);
        string lang = li <= 0 || li - 1 >= langs.Count ? "auto" : langs[li - 1].Code;
        string theme = ThemeChoices[Math.Clamp(Selected(_cbTheme), 0, 2)];
        double font = FontChoices[Math.Clamp(Selected(_cbFont), 0, FontChoices.Length - 1)] / 100.0;
        int handoff = HandoffChoices[Math.Clamp(Selected(_cbHandoff), 0, HandoffChoices.Length - 1)];
        int di = Selected(_cbDevice);
        string? device = di <= 0 || di - 1 >= _dlgDevices.Count ? null : _dlgDevices[di - 1];

        bool langChanged = !lang.Equals(_settings.Language, StringComparison.OrdinalIgnoreCase);
        bool themeChanged = !theme.Equals(_settings.Theme, StringComparison.OrdinalIgnoreCase);
        bool fontChanged = Math.Abs(font - _settings.FontScale) > 0.001;

        void Save(string key, System.Text.Json.Nodes.JsonNode? value) => _settings.SaveEffectiveValue(_project, key, value);
        _settings.Language = lang; Save("language", lang);
        _settings.Theme = theme; Save("theme", theme);
        _settings.FontScale = font; Save("fontScale", font);
        _settings.SpeakSent = Checked(_chkSpeakSent); Save("speakSent", _settings.SpeakSent);
        _settings.SpeakReplies = Checked(_chkSpeakReplies); Save("speakReplies", _settings.SpeakReplies);
        _settings.ResumeAfterLimit = Checked(_chkResume); Save("resumeAfterLimit", _settings.ResumeAfterLimit);
        _settings.CheckUpdates = Checked(_chkUpdates); Save("checkUpdates", _settings.CheckUpdates);
        _settings.ContextHandoffPercent = handoff; Save("contextHandoffPercent", handoff);
        if (_dlgRoot != null && !string.Equals(_dlgRoot, ProjectsRoot, StringComparison.OrdinalIgnoreCase))
        { _settings.ProjectsRoot = _dlgRoot; Save("projectsRoot", _dlgRoot); }

        _settings.SoundSend = Checked(_chkSend); Save("soundSend", _settings.SoundSend);
        _settings.SoundBell = Checked(_chkBell); Save("soundBell", _settings.SoundBell);
        _settings.SoundTyping = Checked(_chkTyping); Save("soundTyping", _settings.SoundTyping);
        _settings.KeepAudioDeviceAwake = Checked(_chkKeepAwake); Save("keepAudioDeviceAwake", _settings.KeepAudioDeviceAwake);
        _settings.SoundSendVolume = VolumeOf(_cbVolSend); Save("soundSendVolume", _settings.SoundSendVolume);
        _settings.SoundBellVolume = VolumeOf(_cbVolBell); Save("soundBellVolume", _settings.SoundBellVolume);
        _settings.SoundTypingVolume = VolumeOf(_cbVolTyping); Save("soundTypingVolume", _settings.SoundTypingVolume);
        _settings.AudioDevice = device; Save("audioDevice", device);
        ApplySoundSettings();

        // Instruction files: written only when changed (or new), so Claude reads them next start.
        string personal = Win32.GetText(_edPersonal), chat = Win32.GetText(_edChat);
        foreach (var (path, text, disk) in new[] { (PersonalInstructionsPath, personal, _diskPersonal), (ChatInstructionsPath, chat, _diskChat) })
            if (WriteInstructions(path, text, disk) is { } err) AddEntry(_loc["role.system"], _loc.Format("set.instrError", path, err));
        _diskPersonal = ReadInstructions(PersonalInstructionsPath);
        _diskChat = ReadInstructions(ChatInstructionsPath);

        if (langChanged) _loc.Load(lang);
        if (themeChanged) ApplyTheme();
        if (fontChanged) RecreateFont();
        ApplyTexts(); // labels, menu, layout
        if (_dlg != IntPtr.Zero) _theme.ApplyToTopLevel(_dlg);
    }

    private void ChooseProjectsRoot()
    {
        string? folder = Program.PickFolder(_dlg, _loc["set.projects"], _dlgRoot);
        if (folder == null) return;
        _dlgRoot = folder;
        Win32.SetWindowTextW(_edRoot, folder);
    }

    // ------------------------------------------------------------------ dialog procedure

    private static IntPtr SettingsProcStatic(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var self = s_instance;
        if (self != null && hwnd == self._dlg)
        {
            switch (msg)
            {
                case Win32.WM_COMMAND:
                    int id = Win32.LoWord(wParam);
                    switch (id)
                    {
                        case IdOk: self.ApplySettingsFromDialog(); self._dlgClose = true; return IntPtr.Zero;
                        case IdCancel: self._dlgClose = true; return IntPtr.Zero;
                        case IdApply: self.ApplySettingsFromDialog(); return IntPtr.Zero;
                        case IdChangeRoot: self.ChooseProjectsRoot(); return IntPtr.Zero;
                        case IdEditProject: self.EditProjectInstructions(); return IntPtr.Zero;
                        case IdListenSend: self.PreviewSound("send", VolumeOf(self._cbVolSend)); return IntPtr.Zero;
                        case IdListenBell: self.PreviewSound("bell", VolumeOf(self._cbVolBell)); return IntPtr.Zero;
                        case IdListenTyping: self.PreviewSound("typing", VolumeOf(self._cbVolTyping)); return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_NOTIFY:
                    var nm = Marshal.PtrToStructure<Win32.NMHDR>(lParam);
                    if (nm.hwndFrom == self._tab && nm.code == Win32.TCN_SELCHANGE)
                        self.ShowPage((int)Win32.SendMessageW(self._tab, Win32.TCM_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
                    break;
                case Win32.WM_CLOSE:
                    self._dlgClose = true;
                    return IntPtr.Zero;
                case 0x0006: // WM_ACTIVATE: after Alt+Tab, focus goes back where it was, not to the tab strip
                    if (Win32.LoWord(wParam) == 0)
                    {
                        IntPtr f = Win32.GetFocus();
                        if (f != IntPtr.Zero && Win32.IsChild(hwnd, f)) self._dlgFocus = f;
                    }
                    else if (self._dlgFocus != IntPtr.Zero && Win32.IsWindow(self._dlgFocus))
                    {
                        Win32.SetFocus(self._dlgFocus);
                        return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_ERASEBKGND:
                    Win32.GetClientRect(hwnd, out RECT rc);
                    Win32.FillRect(wParam, ref rc, self._theme.WindowBrush);
                    return (IntPtr)1;
                case Win32.WM_CTLCOLORSTATIC:
                    if (lParam == self._edRoot)
                    {
                        Win32.SetTextColor(wParam, self._theme.FieldText);
                        Win32.SetBkColor(wParam, self._theme.ReadOnlyFieldBack);
                        return self._theme.ReadOnlyFieldBrush;
                    }
                    Win32.SetTextColor(wParam, self._theme.LabelText);
                    Win32.SetBkColor(wParam, self._theme.WindowBack);
                    return self._theme.WindowBrush;
                case Win32.WM_CTLCOLOREDIT:
                case Win32.WM_CTLCOLORLISTBOX:
                    Win32.SetTextColor(wParam, self._theme.FieldText);
                    Win32.SetBkColor(wParam, self._theme.FieldBack);
                    return self._theme.FieldBrush;
                case Win32.WM_CTLCOLORBTN:
                    Win32.SetTextColor(wParam, self._theme.WindowText);
                    Win32.SetBkColor(wParam, self._theme.WindowBack);
                    return self._theme.WindowBrush;
            }
        }
        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
