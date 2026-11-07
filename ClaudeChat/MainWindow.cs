using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// The classic Win32 window: title bar, menu bar, History (top third, full width),
/// a strip with the check box and buttons, then Message (with Send on its right) and Live,
/// which share the rest equally. Only standard controls, so screen readers know them all.
/// </summary>
internal sealed partial class MainWindow
{
    // Control ids (1 and 2 are IDOK / IDCANCEL, which IsDialogMessage sends for Enter / Escape).
    private const int IdHistoryLabel = 100, IdHistory = 101;
    private const int IdStop = 112;
    private const int IdCtxLabel = 140, IdCtx = 141, IdFiveLabel = 142, IdFive = 143, IdWeekLabel = 144, IdWeek = 145;
    private const int IdModelLabel = 150, IdModel = 151, IdEffortLabel = 152, IdEffort = 153, IdPermLabel = 154, IdPerm = 155;

    // Combo choices: the value passed to Claude Code (null = its own default) and the text key.
    private static readonly (string? Value, string Key)[] ModelChoices =
    {
        (null, "choice.default"), ("opus", "model.opus"), ("sonnet", "model.sonnet"),
        ("haiku", "model.haiku"), ("fable", "model.fable"),
    };
    private static readonly (string? Value, string Key)[] EffortChoices =
    {
        (null, "choice.default"), ("low", "effort.low"), ("medium", "effort.medium"),
        ("high", "effort.high"), ("xhigh", "effort.xhigh"), ("max", "effort.max"),
    };
    private static readonly (string? Value, string Key)[] PermissionChoices =
    {
        ("auto", "perm.auto"), ("acceptEdits", "perm.acceptEdits"), ("manual", "perm.manual"),
        ("dontAsk", "perm.dontAsk"), ("plan", "perm.plan"), ("bypassPermissions", "perm.bypass"),
    };
    private const int IdmResume = 240, IdmHandoff = 241;
    private const int IdInputLabel = 120, IdInput = 121, IdSend = 122;
    private const int IdLiveLabel = 130, IdLive = 131;
    // Menu ids
    private const int IdmOpen = 200, IdmNew = 201, IdmExit = 202, IdmExplore = 203, IdmSettings = 204;
    private const int IdmThemeSystem = 210, IdmThemeLight = 211, IdmThemeDark = 212;
    private const int IdmResetSize = 220;
    private const int IdmSpeakSent = 230, IdmSpeakReplies = 231;
    private const int IdmKeys = 400, IdmAbout = 401;
    private const int IdmLanguageBase = 300; // 300..399
    private const int IdmTargetBase = 1000;  // File → Chat / Code entries, 1000..

    private const uint WmPump = Win32.WM_APP + 1;
    private const int TimerLimit = 1, TimerStop = 2, TimerRestart = 3;
    private const string MainClass = "ClaudeChat.Main";
    private const string ReaderClass = "ClaudeChat.Reader";

    private static MainWindow? s_instance;
    private static readonly WndProcDelegate s_mainProc = MainProcStatic;
    private static readonly WndProcDelegate s_readerProc = ReaderProcStatic;

    private readonly string _userFile;
    private readonly JsonObject _cli;
    private readonly AppState _state;
    private readonly Localization _loc;
    private Settings _settings;
    private string _project;
    private Theme _theme;
    private ISpeech _speech;

    private IntPtr _hwnd, _hInst, _font, _menu;
    private IntPtr _lblHistory, _history, _btnStop;
    private IntPtr _lblCtx, _ctx, _lblFive, _five, _lblWeek, _week;
    private IntPtr _lblModel, _cbModel, _lblEffort, _cbEffort, _lblPerm, _cbPerm;
    private bool _restartAfterTurn;
    private Handoff _handoff = Handoff.None;
    private bool _handoffWarned;
    private int _ctxPercent;
    private string _ctxText = "—", _fiveText = "—", _weekText = "—";
    private IntPtr _lblInput, _input, _btnSend, _lblLive, _live;
    private IntPtr _reader, _readerEdit, _focusBeforeReader, _lastFocus;
    private uint _dpi = 96;

    private readonly ConcurrentQueue<Action> _uiQueue = new();
    private readonly List<HistoryEntry> _entries = new();
    /// <summary>Where each entry starts in the History field (character index), for the context menu.</summary>
    private readonly List<int> _entryStarts = new();
    private readonly List<string> _queue = new();
    private IReadOnlyList<Localization.LanguageInfo> _languages = Array.Empty<Localization.LanguageInfo>();

    private ClaudeSession? _session;
    private string? _lastSessionId;
    private string? _claudeExe;
    private bool _busy, _stopping, _forceNew, _startedOnce;
    private readonly StringBuilder _turnText = new();
    private readonly StringBuilder _liveLine = new();
    private DateTimeOffset? _resumeAt;
    private ResultInfo? _lastLimit;

    private bool _swallowSysChar;
    private bool _sendAltClick;
    private IntPtr _fileMenu;
    private sealed record MenuTarget(string Folder, string? SessionId, bool New, string Label, bool Explore = false);
    private readonly List<MenuTarget> _menuTargets = new();

    private sealed record HistoryEntry(string Role, DateTime Time, string Text);

    public MainWindow(string userFile, JsonObject cli, AppState state, Localization loc, string project)
    {
        _userFile = userFile;
        _cli = cli;
        _state = state;
        _loc = loc;
        _project = project;
        _settings = Settings.Load(userFile, project, cli);
        _loc.Load(_settings.Language);
        _theme = Theme.Create(_settings.Theme);
        _speech = SpeechFactory.Create(_settings.Speech);
        s_instance = this;
    }

    // ================================================================== creation and loop

    public int Run()
    {
        _hInst = Win32.GetModuleHandleW(null);
        RegisterClass(MainClass, s_mainProc);
        RegisterClass(ReaderClass, s_readerProc);

        uint exStyle = Win32.WS_EX_CONTROLPARENT | (_loc.RightToLeft ? Win32.WS_EX_LAYOUTRTL : 0);
        var (x, y, w, h) = TargetPlacement();
        _hwnd = Win32.CreateWindowExW(exStyle, MainClass, _settings.AppName,
            Win32.WS_OVERLAPPEDWINDOW | Win32.WS_CLIPCHILDREN, x, y, w, h,
            IntPtr.Zero, IntPtr.Zero, _hInst, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) return 1;

        _dpi = Win32.GetDpiForWindow(_hwnd);
        CreateControls();
        RecreateFont();
        ApplyTexts();
        ApplyTheme();

        Win32.ShowWindow(_hwnd, Win32.SW_SHOWNORMAL);
        // Always show focus rectangles and underlined access keys (UIS_CLEAR, HIDEFOCUS|HIDEACCEL).
        Win32.SendMessageW(_hwnd, Win32.WM_CHANGEUISTATE, (IntPtr)((3 << 16) | 2), IntPtr.Zero);
        Win32.UpdateWindow(_hwnd);
        CompensateInvisibleBorders(x, y, w, h);
        Win32.SetFocus(_input);

        _claudeExe = ClaudeLocator.Find(_settings.ClaudePath);
        SetLive(_loc["live.idle"]);
        _state.HiddenProjects.RemoveAll(h => SameFolder(h, _project)); // opened again: back in the list
        _state.LastProject = _project;
        _state.Save();
        // History first: loading it clears the field, so start-up notes come after it.
        LoadHistory();
        if (_settings.Problems.Count > 0)
            AddEntry(_loc["role.system"], _loc.Format("error.settings", string.Join(Environment.NewLine, _settings.Problems)));
        if (_claudeExe == null)
        {
            AddEntry(_loc["role.system"], _loc.Format("error.noClaude", _settings.UserFile));
            Win32.PostMessageW(_hwnd, Win32.WM_COMMAND, (IntPtr)IdmInstallClaude, IntPtr.Zero); // once the window shows
        }
        if (_hookFailed)
            AddEntry(_loc["role.system"], _loc.Format("history.error", "History context menu: SetWindowSubclass failed"));
        RestoreDraft();
        InitSounds();
        try { File.Delete(RestartFlag); } catch { } // a request older than this window is stale
        Win32.SetTimer(_hwnd, (IntPtr)TimerRestart, 2000, IntPtr.Zero);

        while (Win32.GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (PreTranslate(ref msg)) continue;
            IntPtr root = Win32.GetAncestor(msg.hwnd, Win32.GA_ROOT);
            if (root != IntPtr.Zero && Win32.IsDialogMessageW(root, ref msg)) continue;
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }
        _session?.Kill();
        return 0;
    }

    private void RegisterClass(string name, WndProcDelegate proc)
    {
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(proc),
            hInstance = _hInst,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)Win32.IDC_ARROW),
            // The exe's own icon (ApplicationIcon, resource 32512), else the system default.
            hIcon = Win32.LoadIconW(_hInst, (IntPtr)32512) is { } own && own != IntPtr.Zero ? own : Win32.LoadIconW(IntPtr.Zero, (IntPtr)Win32.IDI_APPLICATION),
            lpszClassName = name,
        };
        Win32.RegisterClassExW(ref wc);
    }

    private IntPtr Child(string cls, uint style, uint exStyle, int id) =>
        Win32.CreateWindowExW(exStyle, cls, "", Win32.WS_CHILD | Win32.WS_VISIBLE | style,
            0, 0, 0, 0, _hwnd, (IntPtr)id, _hInst, IntPtr.Zero);

    /// <summary>
    /// Creation order is the Tab order (and the order screen readers walk), independent of where
    /// things stand on screen (author, 2026-11-08): Message, Send, the six indicators, Live, Stop,
    /// History, and round to Message. Each label is created right before its control so screen
    /// readers name the control after it.
    /// </summary>
    private void CreateControls()
    {
        const uint field = Win32.ES_MULTILINE | Win32.ES_AUTOVSCROLL | Win32.WS_VSCROLL | Win32.WS_TABSTOP;
        _lblInput = Child("STATIC", Win32.SS_LEFT, 0, IdInputLabel);
        _input = Child("EDIT", field | Win32.ES_WANTRETURN, Win32.WS_EX_CLIENTEDGE, IdInput);
        _btnSend = Child("BUTTON", Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP | Win32.BS_MULTILINE, 0, IdSend);

        const uint indicator = Win32.ES_READONLY | Win32.ES_AUTOHSCROLL | Win32.WS_TABSTOP;
        _lblCtx = Child("STATIC", Win32.SS_LEFT | Win32.SS_NOPREFIX, 0, IdCtxLabel);
        _ctx = Child("EDIT", indicator, Win32.WS_EX_CLIENTEDGE, IdCtx);
        _lblFive = Child("STATIC", Win32.SS_LEFT | Win32.SS_NOPREFIX, 0, IdFiveLabel);
        _five = Child("EDIT", indicator, Win32.WS_EX_CLIENTEDGE, IdFive);
        _lblWeek = Child("STATIC", Win32.SS_LEFT | Win32.SS_NOPREFIX, 0, IdWeekLabel);
        _week = Child("EDIT", indicator, Win32.WS_EX_CLIENTEDGE, IdWeek);
        const uint combo = Win32.CBS_DROPDOWNLIST | Win32.WS_VSCROLL | Win32.WS_TABSTOP;
        _lblModel = Child("STATIC", Win32.SS_LEFT | Win32.SS_NOPREFIX, 0, IdModelLabel);
        _cbModel = Child("COMBOBOX", combo, 0, IdModel);
        _lblEffort = Child("STATIC", Win32.SS_LEFT | Win32.SS_NOPREFIX, 0, IdEffortLabel);
        _cbEffort = Child("COMBOBOX", combo, 0, IdEffort);
        _lblPerm = Child("STATIC", Win32.SS_LEFT | Win32.SS_NOPREFIX, 0, IdPermLabel);
        _cbPerm = Child("COMBOBOX", combo, 0, IdPerm);

        _lblLive = Child("STATIC", Win32.SS_LEFT, 0, IdLiveLabel);
        _live = Child("EDIT", field | Win32.ES_READONLY | Win32.ES_NOHIDESEL, Win32.WS_EX_CLIENTEDGE, IdLive);
        _btnStop = Child("BUTTON", Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP | Win32.BS_MULTILINE, 0, IdStop);
        _lblHistory = Child("STATIC", Win32.SS_LEFT, 0, IdHistoryLabel);
        _history = Child("EDIT", field | Win32.ES_READONLY | Win32.ES_NOHIDESEL, Win32.WS_EX_CLIENTEDGE, IdHistory);

        foreach (var e in new[] { _history, _input, _live })
            Win32.SendMessageW(e, Win32.EM_SETLIMITTEXT, IntPtr.Zero, IntPtr.Zero); // no 30 000-character cap
        HookHistoryMenu();
        HookInputMenu();
    }

    private IEnumerable<IntPtr> AllControls() => new[]
    {
        _lblHistory, _history, _btnStop,
        _lblCtx, _ctx, _lblFive, _five, _lblWeek, _week,
        _lblModel, _cbModel, _lblEffort, _cbEffort, _lblPerm, _cbPerm,
        _lblInput, _input, _btnSend, _lblLive, _live,
    };

    // ================================================================== texts, menu, theme, font

    private void ApplyTexts()
    {
        ApplyConversationOverrides();
        string folder = Path.GetFileName(_project.TrimEnd('\\')) is { Length: > 0 } n ? n : _project;
        // In its own folder the window is titled once ("Nemoviz CC"), elsewhere "Project — Nemoviz CC".
        Win32.SetWindowTextW(_hwnd, folder.Equals(_settings.AppName, StringComparison.OrdinalIgnoreCase)
            ? _settings.AppName
            : _loc.Format("app.title", folder, _settings.AppName));
        Win32.SetWindowTextW(_lblHistory, _loc["label.history"]);
        Win32.SetWindowTextW(_btnStop, _loc["button.stop"]);
        Win32.SetWindowTextW(_lblInput, InputLabelText());
        Win32.SetWindowTextW(_btnSend, _loc["button.send"]);
        Win32.SetWindowTextW(_lblLive, _loc["label.live"]);
        Win32.SetWindowTextW(_lblCtx, _loc["label.context"]);
        Win32.SetWindowTextW(_lblFive, _loc["label.fiveHour"]);
        Win32.SetWindowTextW(_lblWeek, _loc["label.week"]);
        Win32.SetWindowTextW(_lblModel, _loc["label.model"]);
        Win32.SetWindowTextW(_lblEffort, _loc["label.effort"]);
        Win32.SetWindowTextW(_lblPerm, _loc["label.permissions"]);
        ShowIndicators();
        FillCombo(_cbModel, ModelChoices, _settings.Model);
        FillCombo(_cbEffort, EffortChoices, _settings.Effort);
        FillCombo(_cbPerm, PermissionChoices, _settings.PermissionMode ?? "auto");
        BuildMenu();
        Layout();
    }

    private void BuildMenu()
    {
        IntPtr bar = Win32.CreateMenu();

        IntPtr file = Win32.CreatePopupMenu();
        _fileMenu = file;
        _menuTargets.Clear();
        Win32.AppendMenuW(file, Win32.MF_POPUP, BuildChatMenu(), _loc["menu.file.chat"]);
        Win32.AppendMenuW(file, Win32.MF_POPUP, BuildCodeMenu(), _loc["menu.file.code"]);
        Win32.AppendMenuW(file, Win32.MF_STRING, IdmOrganizer, _loc["menu.file.organizer"]);
        Win32.AppendMenuW(file, Win32.MF_SEPARATOR, IntPtr.Zero, null);
        Win32.AppendMenuW(file, Win32.MF_STRING, IdmExplore, _loc["menu.file.explore"]);
        Win32.AppendMenuW(file, Win32.MF_STRING, IdmSettings, _loc["menu.file.settings"]);
        Win32.AppendMenuW(file, Win32.MF_STRING, IdmLogin, _loc["menu.file.login"]);
        Win32.AppendMenuW(file, Win32.MF_SEPARATOR, IntPtr.Zero, null);
        Win32.AppendMenuW(file, Win32.MF_STRING, IdmExit, _loc["menu.file.exit"]);
        Win32.AppendMenuW(bar, Win32.MF_POPUP, file, _loc["menu.file"]);

        // Session: what concerns the conversation in the current folder (author, 2026-11-08:
        // New conversation and Choose project folder read clearer here than under File).
        IntPtr session = Win32.CreatePopupMenu();
        Win32.AppendMenuW(session, Win32.MF_STRING, IdmNew, _loc["menu.file.new"]);
        Win32.AppendMenuW(session, Win32.MF_STRING, IdmOpen, _loc["menu.file.open"]);
        Win32.AppendMenuW(session, Win32.MF_SEPARATOR, IntPtr.Zero, null);
        Win32.AppendMenuW(session, Win32.MF_STRING | (_settings.ResumeAfterLimit ? Win32.MF_CHECKED : 0), IdmResume, _loc["menu.session.resume"]);
        Win32.AppendMenuW(session, Win32.MF_STRING, IdmHandoff, _loc["menu.session.handoff"]);
        Win32.AppendMenuW(session, Win32.MF_SEPARATOR, IntPtr.Zero, null);
        Win32.AppendMenuW(session, Win32.MF_STRING, IdmDeleteCurrent, _loc["menu.session.delete"]);
        Win32.AppendMenuW(bar, Win32.MF_POPUP, session, _loc["menu.session"]);

        // View and Speech are gone (author, 2026-11-07): theme, language and window size live in
        // Settings → General, speech in Settings → Sounds and devices.

        IntPtr help = Win32.CreatePopupMenu();
        Win32.AppendMenuW(help, Win32.MF_STRING, IdmKeys, _loc["menu.help.keys"]);
        Win32.AppendMenuW(help, Win32.MF_STRING, IdmAbout, _loc["menu.help.about"]);
        Win32.AppendMenuW(bar, Win32.MF_POPUP, help, _loc["menu.help"]);

        IntPtr old = _menu;
        Win32.SetMenu(_hwnd, bar);
        _menu = bar;
        if (old != IntPtr.Zero) Win32.DestroyMenu(old);
        Win32.DrawMenuBar(_hwnd);
    }

    private void ApplyTheme()
    {
        var old = _theme;
        _theme = Theme.Create(_settings.Theme);
        old.Dispose();

        foreach (var top in new[] { _hwnd, _reader })
            if (top != IntPtr.Zero) _theme.ApplyToTopLevel(top);
        foreach (var c in new[] { _history, _input, _live, _readerEdit })
            if (c != IntPtr.Zero) _theme.ApplyToControl(c, Theme.ControlRole.Field);
        foreach (var b in new[] { _btnStop, _btnSend })
            _theme.ApplyToControl(b, Theme.ControlRole.PushButton);
        foreach (var f in new[] { _ctx, _five, _week })
            _theme.ApplyToControl(f, Theme.ControlRole.Field);
        foreach (var c in new[] { _cbModel, _cbEffort, _cbPerm })
            _theme.ApplyToControl(c, Theme.ControlRole.Combo);

        foreach (var top in new[] { _hwnd, _reader })
            if (top != IntPtr.Zero)
                Win32.RedrawWindow(top, IntPtr.Zero, IntPtr.Zero,
                    Win32.RDW_INVALIDATE | Win32.RDW_ERASE | Win32.RDW_ALLCHILDREN | Win32.RDW_FRAME);
    }

    /// <summary>The Windows message font at this window's DPI, times the user's scale.</summary>
    private void RecreateFont()
    {
        var ncm = new NONCLIENTMETRICS { cbSize = (uint)Marshal.SizeOf<NONCLIENTMETRICS>() };
        IntPtr font;
        if (Win32.SystemParametersInfoForDpi(Win32.SPI_GETNONCLIENTMETRICS, ncm.cbSize, ref ncm, 0, _dpi))
        {
            var lf = ncm.lfMessageFont;
            lf.lfHeight = (int)Math.Round(lf.lfHeight * Math.Clamp(_settings.FontScale, 0.5, 4.0));
            font = Win32.CreateFontIndirectW(ref lf);
        }
        else
        {
            var lf = new LOGFONT { lfHeight = -S(12), lfWeight = 400, lfFaceName = "Segoe UI" };
            font = Win32.CreateFontIndirectW(ref lf);
        }

        foreach (var c in AllControls()) Win32.SendMessageW(c, Win32.WM_SETFONT, font, (IntPtr)1);
        if (_readerEdit != IntPtr.Zero) Win32.SendMessageW(_readerEdit, Win32.WM_SETFONT, font, (IntPtr)1);
        if (_font != IntPtr.Zero) Win32.DeleteObject(_font);
        _font = font;
    }

    // ================================================================== geometry

    /// <summary>Device-independent pixels to this window's physical pixels.</summary>
    private int S(int dip) => (int)Math.Round(dip * _dpi / 96.0);

    /// <summary>
    /// Width = ratio of the work area (the screen minus the taskbar, wherever it is), height = ratio
    /// minus the reserve. The process is per-monitor DPI aware, so these are real (physical) pixels,
    /// and the result is the same share of the screen at any resolution and any scaling.
    /// </summary>
    private (int x, int y, int w, int h) TargetPlacement()
    {
        IntPtr mon = _hwnd != IntPtr.Zero
            ? Win32.MonitorFromWindow(_hwnd, Win32.MONITOR_DEFAULTTONEAREST)
            : Win32.MonitorFromPoint(new POINT(), Win32.MONITOR_DEFAULTTOPRIMARY);
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        Win32.GetMonitorInfoW(mon, ref mi);
        uint dpi = Win32.GetDpiForMonitor(mon, Win32.MDT_EFFECTIVE_DPI, out uint dx, out _) == 0 ? dx : 96;

        var ws = _settings.Window;
        RECT work = mi.rcWork;
        int w = (int)Math.Round(work.Width * Math.Clamp(ws.WidthRatio, 0.1, 1.0));
        int reserve = (int)Math.Round(Math.Max(0, ws.BottomReserve) * dpi / 96.0);
        int h = (int)Math.Round(work.Height * Math.Clamp(ws.HeightRatio, 0.1, 1.0)) - reserve;
        int x = ws.Dock.ToLowerInvariant() switch
        {
            "left" => work.Left,
            "center" or "centre" => work.Left + (work.Width - w) / 2,
            _ => work.Right - w,
        };
        return (x, work.Top, w, h);
    }

    /// <summary>
    /// Windows 10/11 frames have invisible resize borders; widen the window by them so the
    /// visible frame, not the invisible one, has the target size and position.
    /// </summary>
    private void CompensateInvisibleBorders(int x, int y, int w, int h)
    {
        if (!Win32.GetWindowRect(_hwnd, out RECT wr)) return;
        if (Win32.DwmGetWindowAttribute(_hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT vis, Marshal.SizeOf<RECT>()) != 0) return;
        int l = vis.Left - wr.Left, t = vis.Top - wr.Top, r = wr.Right - vis.Right, b = wr.Bottom - vis.Bottom;
        if (l == 0 && t == 0 && r == 0 && b == 0) return;
        Win32.SetWindowPos(_hwnd, IntPtr.Zero, x - l, y - t, w + l + r, h + t + b, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    private void ResetPlacement()
    {
        var (x, y, w, h) = TargetPlacement();
        Win32.SetWindowPos(_hwnd, IntPtr.Zero, x, y, w, h, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        CompensateInvisibleBorders(x, y, w, h);
    }

    private SIZE Measure(string text)
    {
        IntPtr dc = Win32.GetDC(_hwnd);
        IntPtr old = Win32.SelectObject(dc, _font);
        string t = text.Replace("&", "");
        Win32.GetTextExtentPoint32W(dc, t.Length == 0 ? "Ag" : t, Math.Max(t.Length, 2), out SIZE size);
        Win32.SelectObject(dc, old);
        Win32.ReleaseDC(_hwnd, dc);
        return size;
    }

    private void Layout()
    {
        if (_hwnd == IntPtr.Zero || _font == IntPtr.Zero) return;
        Win32.GetClientRect(_hwnd, out RECT rc);
        int cx = rc.Width, cy = rc.Height;
        if (cx <= 0 || cy <= 0) return;

        int pad = S(8), gap = S(6);
        int lineH = Measure("Ag").cy;
        int labelH = lineH + S(2);
        int btnH = lineH + S(14);
        int innerW = Math.Max(cx - 2 * pad, S(40));
        int minField = S(32);
        // Top to bottom (author, 2026-11-08): the six indicators, Live (three lines, square Stop
        // on its right), History (all the rest), Message (three lines, square Send on its right).

        // Indicators: two rows of three columns, each a label over its control.
        int ctrlH = lineH + S(10);
        int rowH = labelH + ctrlH;
        int colW = (innerW - 2 * gap) / 3;
        int y = pad;
        void Cell(int row, int col, IntPtr label, IntPtr control, bool isCombo)
        {
            int x = pad + col * (colW + gap), top = y + row * (rowH + gap);
            Move(label, x, top, colW, labelH);
            // A drop-down combo's height includes its open list.
            Move(control, x, top + labelH, colW, isCombo ? ctrlH + S(220) : ctrlH);
        }
        Cell(0, 0, _lblCtx, _ctx, false);
        Cell(0, 1, _lblFive, _five, false);
        Cell(0, 2, _lblWeek, _week, false);
        Cell(1, 0, _lblModel, _cbModel, true);
        Cell(1, 1, _lblEffort, _cbEffort, true);
        Cell(1, 2, _lblPerm, _cbPerm, true);
        y += 2 * rowH + gap + gap;

        // Live and Message are three lines high; Stop and Send are squares of that height —
        // wider only if their text would not fit.
        int boxH = 3 * lineH + S(10);
        int btnW = Math.Max(boxH, Math.Max(Measure(Win32.GetText(_btnSend)).cx, Measure(Win32.GetText(_btnStop)).cx) + S(20));
        int fieldW = Math.Max(innerW - btnW - gap, S(40));
        int btnX = cx - pad - btnW;

        Move(_lblLive, pad, y, innerW, labelH);
        y += labelH;
        Move(_live, pad, y, fieldW, boxH);
        Move(_btnStop, btnX, y, btnW, boxH);
        y += boxH + gap;

        // Message sits at the bottom; History takes everything between Live and Message.
        int inputTop = cy - pad - boxH;
        int inputLabelTop = inputTop - labelH;
        Move(_lblInput, pad, inputLabelTop, innerW, labelH);
        Move(_input, pad, inputTop, fieldW, boxH);
        Move(_btnSend, btnX, inputTop, btnW, boxH);

        Move(_lblHistory, pad, y, innerW, labelH);
        y += labelH;
        Move(_history, pad, y, innerW, Math.Max(inputLabelTop - gap - y, minField));
    }

    // ================================================================== bottom strip

    private void ShowIndicators()
    {
        if (Win32.GetText(_ctx) != _ctxText) Win32.SetWindowTextW(_ctx, _ctxText);
        if (Win32.GetText(_five) != _fiveText) Win32.SetWindowTextW(_five, _fiveText);
        if (Win32.GetText(_week) != _weekText) Win32.SetWindowTextW(_week, _weekText);
    }

    private void OnContext(long used, long window)
    {
        _ctxPercent = (int)Math.Round(100.0 * used / window);
        _ctxText = _loc.Format("ind.context", _ctxPercent, used / 1000, window / 1000);
        ShowIndicators();

        // Past the threshold: warn once, and hand over after this answer (see OnResult).
        int limit = _settings.ContextHandoffPercent;
        if (limit > 0 && _ctxPercent >= limit && _handoff == Handoff.None && !_handoffWarned)
        {
            _handoffWarned = true;
            _handoff = Handoff.Due;
            string note = _loc.Format("handoff.warning", _ctxPercent);
            AddEntry(_loc["role.system"], note);
            _speech.Speak(note, false);
        }
    }

    // ================================================================== hand-over before the context fills

    /// <summary>None → Due (threshold crossed or asked for) → Writing (Claude saves memory, commits,
    /// writes the hand-over) → a new conversation in the same folder that reads it.</summary>
    private enum Handoff { None, Due, Writing }

    private string HandoffFile => Path.Combine(_project, ".claude", "handoff.md");

    private void StartHandoffWriting()
    {
        _handoff = Handoff.Writing;
        string prompt = _ctxPercent > 0
            ? _loc.Format("handoff.prompt", _ctxPercent, HandoffFile)
            : _loc.Format("handoff.promptManual", HandoffFile);
        Dispatch(prompt);
        AddLive(_loc["live.handoff"]);
    }

    private void StartAfterHandoff()
    {
        _handoff = Handoff.None;
        string file = HandoffFile;
        SwitchTo(_project, null, newConversation: true, null);
        Dispatch(_loc.Format("handoff.resume", file));
    }

    /// <summary>Session → Hand over: now if idle, otherwise right after the current answer.</summary>
    private void RequestHandoff()
    {
        if (_handoff != Handoff.None) return;
        if (_busy || _stopping) { _handoff = Handoff.Due; AddLive(_loc["live.handoffAfter"]); return; }
        StartHandoffWriting();
    }

    private void OnLimits(double? five, DateTimeOffset? fiveAt, double? week, DateTimeOffset? weekAt)
    {
        // "t" alone is the short-time pattern; inside a custom pattern it would be the AM/PM letter
        // (that is how "čet p" came out), so the weekday and the time are formatted separately.
        string When(DateTimeOffset at, bool withDay)
        {
            var local = at.ToLocalTime();
            string time = local.ToString("t", _loc.Culture);
            return withDay ? local.ToString("dddd", _loc.Culture) + " " + time : time;
        }
        string Text(double? u, DateTimeOffset? at, bool withDay) => u == null ? "—"
            : at == null ? _loc.Format("ind.percent", Math.Round(u.Value * 100))
            : _loc.Format("ind.limit", Math.Round(u.Value * 100), When(at.Value, withDay));
        if (five != null) _fiveText = Text(five, fiveAt, false);
        if (week != null) _weekText = Text(week, weekAt, true);
        ShowIndicators();
    }

    private void FillCombo(IntPtr combo, (string? Value, string Key)[] choices, string? current)
    {
        Win32.SendMessageW(combo, Win32.CB_RESETCONTENT, IntPtr.Zero, IntPtr.Zero);
        int selected = 0, widest = 0;
        for (int i = 0; i < choices.Length; i++)
        {
            string text = _loc[choices[i].Key];
            Win32.SendMessageW(combo, Win32.CB_ADDSTRING, IntPtr.Zero, text);
            widest = Math.Max(widest, Measure(text).cx);
            if (string.Equals(choices[i].Value, current, StringComparison.OrdinalIgnoreCase)) selected = i;
        }
        Win32.SendMessageW(combo, Win32.CB_SETCURSEL, (IntPtr)selected, IntPtr.Zero);
        // The open list is as wide as its longest item, even when the closed combo is narrower.
        Win32.SendMessageW(combo, 0x0160 /* CB_SETDROPPEDWIDTH */, (IntPtr)(widest + S(24)), IntPtr.Zero);
    }

    /// <summary>
    /// A combo changed: remember it (where it takes effect) and let Claude Code restart with it.
    /// The conversation is not lost — the next message resumes the same session with the new
    /// model, effort or permission mode. During an answer the restart waits for it to finish.
    /// </summary>
    private void OnComboChanged(int id)
    {
        IntPtr combo = id == IdModel ? _cbModel : id == IdEffort ? _cbEffort : _cbPerm;
        var choices = id == IdModel ? ModelChoices : id == IdEffort ? EffortChoices : PermissionChoices;
        int i = (int)Win32.SendMessageW(combo, Win32.CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
        if (i < 0 || i >= choices.Length) return;
        string? value = choices[i].Value;
        string key;
        if (id == IdModel) { _settings.Model = value; key = "model"; }
        else if (id == IdEffort) { _settings.Effort = value; key = "effort"; }
        else { _settings.PermissionMode = value; key = "permissionMode"; }
        // A chat made with New chat keeps its own model and effort.
        if (id != IdPerm && CurrentMeta() is { } meta)
        {
            if (id == IdModel) meta.Model = value; else meta.Effort = value;
            _state.Save();
        }
        else _settings.SaveEffectiveValue(_project, key, value);
        RestartClaudeWhenIdle();
    }

    private void RestartClaudeWhenIdle()
    {
        if (_busy || _stopping) { _restartAfterTurn = true; return; }
        _restartAfterTurn = false;
        if (_session == null) return;
        _session.Kill();
        _session = null;
    }

    private static void Move(IntPtr h, int x, int y, int w, int hgt) => Win32.MoveWindow(h, x, y, Math.Max(w, 0), Math.Max(hgt, 0), true);

    // ================================================================== window procedure

    private static IntPtr MainProcStatic(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var self = s_instance;
        if (self != null && (self._hwnd == IntPtr.Zero || self._hwnd == hwnd))
        {
            if (self._hwnd == IntPtr.Zero) self._hwnd = hwnd;
            try
            {
                IntPtr? r = self.MainProc(msg, wParam, lParam);
                if (r.HasValue) return r.Value;
            }
            catch (Exception ex)
            {
                self.AddEntry(self._loc["role.system"], self._loc.Format("history.error", ex.Message));
            }
        }
        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private IntPtr? MainProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_SIZE:
                Layout();
                return IntPtr.Zero;

            case Win32.WM_ERASEBKGND:
                Win32.GetClientRect(_hwnd, out RECT rc);
                Win32.FillRect(wParam, ref rc, _theme.WindowBrush);
                return (IntPtr)1;

            case Win32.WM_CTLCOLOREDIT:
                Win32.SetTextColor(wParam, _theme.FieldText);
                Win32.SetBkColor(wParam, _theme.FieldBack);
                return _theme.FieldBrush;

            case Win32.WM_CTLCOLORSTATIC:
                if (lParam == _history || lParam == _live || lParam == _ctx || lParam == _five || lParam == _week)
                {
                    Win32.SetTextColor(wParam, _theme.FieldText);
                    Win32.SetBkColor(wParam, _theme.ReadOnlyFieldBack);
                    return _theme.ReadOnlyFieldBrush;
                }
                Win32.SetTextColor(wParam, _theme.LabelText);
                Win32.SetBkColor(wParam, _theme.WindowBack);
                return _theme.WindowBrush;

            case Win32.WM_CTLCOLORLISTBOX:
                Win32.SetTextColor(wParam, _theme.FieldText);
                Win32.SetBkColor(wParam, _theme.FieldBack);
                return _theme.FieldBrush;

            case Win32.WM_CTLCOLORBTN:
                Win32.SetTextColor(wParam, _theme.WindowText);
                Win32.SetBkColor(wParam, _theme.WindowBack);
                return _theme.WindowBrush;

            case Win32.WM_ACTIVATE:
                // Keep the focus where the user left it when the window is left and re-entered.
                if (Win32.LoWord(wParam) == 0)
                {
                    IntPtr f = Win32.GetFocus();
                    if (f != IntPtr.Zero && Win32.GetAncestor(f, Win32.GA_ROOT) == _hwnd) _lastFocus = f;
                }
                else
                {
                    Win32.SetFocus(_lastFocus != IntPtr.Zero && Win32.IsWindow(_lastFocus) ? _lastFocus : _input);
                }
                return IntPtr.Zero;

            case Win32.WM_COMMAND:
                OnCommand(Win32.LoWord(wParam), Win32.HiWord(wParam));
                return IntPtr.Zero;

            case Win32.WM_DROPFILES:
                OnDropFiles(wParam);
                return IntPtr.Zero;

            case Win32.WM_INITMENUPOPUP:
                // The conversation lists are read again each time File opens, so they are current.
                if (wParam == _fileMenu) RefreshConversationMenus();
                return null;

            case Win32.WM_TIMER:
                OnTimer((int)wParam);
                return IntPtr.Zero;

            case WmPump:
                while (_uiQueue.TryDequeue(out var action)) action();
                return IntPtr.Zero;

            case Win32.WM_DPICHANGED:
            {
                _dpi = (uint)Win32.HiWord(wParam);
                var r = Marshal.PtrToStructure<RECT>(lParam);
                Win32.SetWindowPos(_hwnd, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                RecreateFont();
                Layout();
                return IntPtr.Zero;
            }

            case Win32.WM_DISPLAYCHANGE:
                ResetPlacement();
                return IntPtr.Zero;

            case Win32.WM_SETTINGCHANGE:
            {
                string area = lParam != IntPtr.Zero ? Marshal.PtrToStringUni(lParam) ?? "" : "";
                uint action = (uint)(long)wParam;
                if (area == "ImmersiveColorSet" || action == 0x0043 /* SPI_SETHIGHCONTRAST */) ApplyTheme();
                if (action == 0x002A /* SPI_SETNONCLIENTMETRICS */) { RecreateFont(); Layout(); }
                if (action == 0x002F /* SPI_SETWORKAREA */) ResetPlacement();
                return null;
            }

            case Win32.WM_SYSCOLORCHANGE:
            case Win32.WM_THEMECHANGED:
                ApplyTheme();
                return null;

            case Win32.WM_CLOSE:
                Win32.DestroyWindow(_hwnd);
                return IntPtr.Zero;

            case Win32.WM_DESTROY:
                SaveDraft();
                _audio.Dispose();
                _session?.Kill();
                _session = null;
                Win32.PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return null;
    }

    private void OnCommand(int id, int code)
    {
        if (id >= IdmTargetBase && id - IdmTargetBase < _menuTargets.Count)
        {
            var t = _menuTargets[id - IdmTargetBase];
            if (t.Explore) ShowInExplorer(t.Folder);
            else SwitchTo(t.Folder, t.SessionId, t.New, t.Label);
            return;
        }

        if (id >= IdmLanguageBase && id < IdmLanguageBase + 100)
        {
            int i = id - IdmLanguageBase;
            if (i < _languages.Count) ChangeLanguage(_languages[i].Code);
            return;
        }

        switch (id)
        {
            // One button: a click sends, Alt+click queues (as Enter / Alt+Enter in the Message field).
            case IdSend when code == Win32.BN_CLICKED:
            {
                bool queue = _sendAltClick; // only a mouse click with Alt; Alt+A (the button's own key) sends
                _sendAltClick = false;
                SendFromInput(queueOnly: queue);
                break;
            }
            case IdStop when code == Win32.BN_CLICKED: Stop(); break;
            case IdmResume: OnResumeToggled(); break;
            case IdmHandoff: RequestHandoff(); break;
            case IdModel or IdEffort or IdPerm when code == Win32.CBN_SELCHANGE: OnComboChanged(id); break;
            case IdmExit: Win32.PostMessageW(_hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); break;
            case IdmOpen: OpenProject(); break;
            case IdmExplore: ShowInExplorer(_project); break;
            case IdmSettings: ShowSettings(); break;
            case IdmNewChat: NewChatWizard(); break;
            case IdmNewProject: NewProjectWizard(); break;
            case IdmOrganizer: ShowOrganizer(); break;
            case IdmLogin: StartLogin(); break;
            case IdmInstallClaude: OfferClaudeInstall(); break;
            case IdmOpenExisting: OpenProject(); break;
            case IdmDeleteCurrent: DeleteCurrentConversation(); break;
            case IdmNew: NewConversation(); break;
            case IdmThemeSystem: ChangeTheme("system"); break;
            case IdmThemeLight: ChangeTheme("light"); break;
            case IdmThemeDark: ChangeTheme("dark"); break;
            case IdmResetSize: ResetPlacement(); break;
            case IdmSpeakSent:
                _settings.SpeakSent = !_settings.SpeakSent;
                _settings.SaveUserValue("speakSent", _settings.SpeakSent);
                BuildMenu();
                break;
            case IdmSpeakReplies:
                _settings.SpeakReplies = !_settings.SpeakReplies;
                _settings.SaveUserValue("speakReplies", _settings.SpeakReplies);
                BuildMenu();
                break;
            case IdmKeys:
                Win32.MessageBoxW(_hwnd, Crlf(_loc["help.keys"]), _loc["help.keys.title"], Win32.MB_OK | Win32.MB_ICONINFORMATION);
                break;
            case IdmAbout:
                string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
                Win32.MessageBoxW(_hwnd,
                    Crlf(_loc.Format("help.about", _settings.AppName, version, _settings.UserFile, _claudeExe ?? "—")),
                    _settings.AppName, Win32.MB_OK | Win32.MB_ICONINFORMATION);
                break;
        }
    }

    private void ChangeTheme(string theme)
    {
        _settings.Theme = theme;
        _settings.SaveUserValue("theme", theme);
        ApplyTheme();
        BuildMenu();
    }

    private void ChangeLanguage(string code)
    {
        _settings.Language = code;
        _settings.SaveUserValue("language", code);
        _loc.Load(code);
        ApplyTexts();
    }

    // ================================================================== keyboard

    /// <summary>Keys handled before the dialog manager and the controls see them.</summary>
    private bool PreTranslate(ref MSG msg)
    {
        IntPtr focus = Win32.GetFocus();
        IntPtr root = msg.hwnd == IntPtr.Zero ? IntPtr.Zero : Win32.GetAncestor(msg.hwnd, Win32.GA_ROOT);
        bool ours = root == _hwnd || (root == _reader && _reader != IntPtr.Zero);
        if (!ours) return false;
        int vk = (int)(long)msg.wParam;

        switch (msg.message)
        {
            case 0x0202: // WM_LBUTTONUP: remember whether Send was clicked with Alt held
                if (msg.hwnd == _btnSend) _sendAltClick = Win32.KeyDown(Win32.VK_MENU);
                break;

            case Win32.WM_KEYDOWN:
                bool ctrl = Win32.KeyDown(Win32.VK_CONTROL), shift = Win32.KeyDown(Win32.VK_SHIFT);
                if (vk == Win32.VK_RETURN && focus == _input && !shift)
                {
                    SendFromInput(queueOnly: false);
                    return true;
                }
                if (vk == Win32.VK_TAB && !ctrl && root == _hwnd)
                {
                    // Multiline EDITs would swallow Tab, so the window moves the focus itself:
                    // every field, check box and button, in creation order. Ctrl+Tab still
                    // reaches the Message field and types a tab character.
                    IntPtr next = Win32.GetNextDlgTabItem(_hwnd, focus, shift);
                    if (next != IntPtr.Zero) Win32.SetFocus(next);
                    return true;
                }
                if (vk == 'H' && !ctrl && focus == _history)
                {
                    JumpMessage(shift ? -1 : 1);
                    return true;
                }
                if (vk == 'A' && ctrl && !shift && IsOurEdit(focus))
                {
                    Win32.SendMessageW(focus, Win32.EM_SETSEL, IntPtr.Zero, (IntPtr)(-1));
                    return true;
                }
                if (focus == _input && ((vk == 'V' && ctrl && !shift) || (vk == 0x2D /* Insert */ && shift && !ctrl)))
                {
                    // Through WM_PASTE, so files and images on the clipboard become attachments.
                    Win32.SendMessageW(_input, Win32.WM_PASTE, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                if (focus == _input && vk == 'U' && ctrl && !shift) // Ctrl+U = upload (Ctrl+O is "open" everywhere else)
                {
                    AttachWithDialog();
                    return true;
                }
                if (vk == Win32.VK_ESCAPE)
                {
                    // In the reader Escape closes it. In the main window it does nothing:
                    // a multiline EDIT would otherwise send WM_CLOSE to its parent and the
                    // whole window, with whatever was being typed, would close.
                    if (root == _reader) CloseReader();
                    return true;
                }
                break;

            case Win32.WM_SYSKEYDOWN:
                if (vk == Win32.VK_RETURN && focus == _input)
                {
                    _swallowSysChar = true;
                    SendFromInput(queueOnly: true);
                    return true;
                }
                if (root == _hwnd && !Win32.KeyDown(Win32.VK_CONTROL) && FieldForKey(vk) is var field && field != IntPtr.Zero)
                {
                    // Alt+H History, Alt+E Edit (Message), Alt+L Live: the same in every language.
                    _swallowSysChar = true;
                    Win32.SetFocus(field);
                    return true;
                }
                if (vk >= '0' && vk <= '9' && !Win32.KeyDown(Win32.VK_CONTROL))
                {
                    _swallowSysChar = true;
                    OnAltDigit(vk - '0');
                    return true;
                }
                break;

            case Win32.WM_SYSCHAR:
                if (_swallowSysChar) { _swallowSysChar = false; return true; }
                break;
        }
        return false;
    }

    private bool IsOurEdit(IntPtr h) => h != IntPtr.Zero && (h == _input || h == _history || h == _live || h == _readerEdit);

    private IntPtr FieldForKey(int vk) => vk switch
    {
        'H' => _history,
        'E' => _input,
        'L' => _live,
        _ => IntPtr.Zero,
    };

    /// <summary>
    /// Alt+1 … Alt+0: speak one of the last ten messages. (Pressing it twice no longer opens a
    /// reader window — History itself is read with the arrows; author, 2026-11-08.)
    /// </summary>
    private void OnAltDigit(int digit)
    {
        int n = digit == 0 ? 10 : digit;
        if (n > _entries.Count)
        {
            _speech.Speak(_loc.Format("speak.none", n), true);
            return;
        }
        var e = _entries[_entries.Count - n];
        string header = Header(e);
        // Without a reachable screen reader, opening the message is the only way to hear it.
        if (!_speech.Speak(header + ". " + e.Text, true))
            OpenReader(n, header, e.Text);
    }

    /// <summary>
    /// H / Shift+H in History: to the start of the next / previous message (like headings in a
    /// browser, without one). The header and first line are spoken; the arrows read on from there.
    /// </summary>
    private void JumpMessage(int direction)
    {
        if (_entryStarts.Count == 0) { Win32.MessageBeep(0); return; }
        Win32.SendMessageW(_history, Win32.EM_GETSEL, out int caret, out _);
        int i = _entryStarts.BinarySearch(caret);
        int current = i >= 0 ? i : ~i - 1; // the message the caret is in (-1 = before the first)
        int target;
        if (direction > 0) target = current + 1;
        // Backwards: inside a message, first to its own start; at its start, to the one before.
        else target = current >= 0 && caret > _entryStarts[current] ? current : current - 1;
        // Past either end it wraps round (author, 2026-11-08) and says so.
        string wrapped = "";
        if (target >= _entryStarts.Count) { target = 0; wrapped = _loc["speak.wrapTop"] + " "; }
        else if (target < 0) { target = _entryStarts.Count - 1; wrapped = _loc["speak.wrapBottom"] + " "; }

        int at = _entryStarts[target];
        Win32.SendMessageW(_history, Win32.EM_SETSEL, (IntPtr)at, (IntPtr)at);
        Win32.SendMessageW(_history, Win32.EM_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);

        var e = _entries[target];
        string first = e.Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        if (first.Length > 200) first = first[..200] + "…";
        _speech.Speak(wrapped + Header(e) + ". " + first, true);
    }

    // ================================================================== reader window

    private void OpenReader(int n, string header, string text)
    {
        CloseReader();
        _focusBeforeReader = Win32.GetFocus();
        Win32.GetWindowRect(_hwnd, out RECT wr);
        _reader = Win32.CreateWindowExW(Win32.WS_EX_CONTROLPARENT | (_loc.RightToLeft ? Win32.WS_EX_LAYOUTRTL : 0),
            ReaderClass, _loc.Format("reader.title", n, header), Win32.WS_OVERLAPPEDWINDOW | Win32.WS_CLIPCHILDREN,
            wr.Left, wr.Top, wr.Width, wr.Height, _hwnd, IntPtr.Zero, _hInst, IntPtr.Zero);
        _readerEdit = Win32.CreateWindowExW(Win32.WS_EX_CLIENTEDGE, "EDIT", Crlf(text),
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.WS_VSCROLL | Win32.ES_MULTILINE |
            Win32.ES_AUTOVSCROLL | Win32.ES_READONLY | Win32.ES_NOHIDESEL,
            0, 0, 0, 0, _reader, (IntPtr)1000, _hInst, IntPtr.Zero);
        Win32.SendMessageW(_readerEdit, Win32.WM_SETFONT, _font, (IntPtr)1);
        _theme.ApplyToTopLevel(_reader);
        _theme.ApplyToControl(_readerEdit, Theme.ControlRole.Field);
        LayoutReader();
        Win32.ShowWindow(_reader, Win32.SW_SHOWNORMAL);
        Win32.SetFocus(_readerEdit);
        Win32.SendMessageW(_readerEdit, Win32.EM_SETSEL, IntPtr.Zero, IntPtr.Zero);
    }

    private void LayoutReader()
    {
        if (_reader == IntPtr.Zero) return;
        Win32.GetClientRect(_reader, out RECT rc);
        int pad = S(8);
        Move(_readerEdit, pad, pad, rc.Width - 2 * pad, rc.Height - 2 * pad);
    }

    private void CloseReader()
    {
        if (_reader == IntPtr.Zero) return;
        IntPtr r = _reader;
        _reader = IntPtr.Zero;
        _readerEdit = IntPtr.Zero;
        Win32.DestroyWindow(r);
        Win32.SetForegroundWindow(_hwnd);
        Win32.SetFocus(_focusBeforeReader != IntPtr.Zero && Win32.IsWindow(_focusBeforeReader) ? _focusBeforeReader : _input);
    }

    private static IntPtr ReaderProcStatic(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var self = s_instance;
        if (self != null)
        {
            switch (msg)
            {
                case Win32.WM_SIZE:
                    if (hwnd == self._reader) self.LayoutReader();
                    return IntPtr.Zero;
                case Win32.WM_ERASEBKGND:
                    Win32.GetClientRect(hwnd, out RECT rc);
                    Win32.FillRect(wParam, ref rc, self._theme.WindowBrush);
                    return (IntPtr)1;
                case Win32.WM_CTLCOLORSTATIC:
                case Win32.WM_CTLCOLOREDIT:
                    Win32.SetTextColor(wParam, self._theme.FieldText);
                    Win32.SetBkColor(wParam, self._theme.ReadOnlyFieldBack);
                    return self._theme.ReadOnlyFieldBrush;
                case Win32.WM_ACTIVATE:
                    if (Win32.LoWord(wParam) != 0 && hwnd == self._reader && self._readerEdit != IntPtr.Zero)
                        Win32.SetFocus(self._readerEdit);
                    return IntPtr.Zero;
                case Win32.WM_COMMAND:
                    if (Win32.LoWord(wParam) == 2 /* IDCANCEL from Escape */) { self.CloseReader(); return IntPtr.Zero; }
                    break;
                case Win32.WM_CLOSE:
                    if (hwnd == self._reader) { self.CloseReader(); return IntPtr.Zero; }
                    break;
            }
        }
        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    // ================================================================== history and live

    private string Header(HistoryEntry e) => _loc.Format("header", e.Role, e.Time.ToString("t", _loc.Culture));

    private static string Crlf(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");

    private void AddEntry(string role, string text)
    {
        var e = new HistoryEntry(role, DateTime.Now, text.Trim());
        _entries.Add(e);
        _entryStarts.Add(Win32.GetWindowTextLengthW(_history));
        AppendToField(_history, Header(e) + "\r\n" + Crlf(e.Text) + "\r\n\r\n");
    }

    /// <summary>
    /// Appends without moving the user: if the field has the focus, the caret and selection stay
    /// exactly where they were; otherwise the field shows the newest text.
    /// </summary>
    private void AppendToField(IntPtr field, string text)
    {
        bool focused = Win32.GetFocus() == field;
        Win32.SendMessageW(field, Win32.EM_GETSEL, out int selStart, out int selEnd);
        int first = (int)Win32.SendMessageW(field, Win32.EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
        int len = Win32.GetWindowTextLengthW(field);
        Win32.SendMessageW(field, Win32.EM_SETSEL, (IntPtr)len, (IntPtr)len);
        Win32.SendMessageW(field, Win32.EM_REPLACESEL, IntPtr.Zero, text);
        if (focused)
        {
            Win32.SendMessageW(field, Win32.EM_SETSEL, (IntPtr)selStart, (IntPtr)selEnd);
            int nowFirst = (int)Win32.SendMessageW(field, Win32.EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            Win32.SendMessageW(field, Win32.EM_LINESCROLL, IntPtr.Zero, (IntPtr)(first - nowFirst));
        }
        else if (field == _live)
        {
            // Live is read top to bottom: away from it, its caret waits at the first line.
            Win32.SendMessageW(field, Win32.EM_SETSEL, IntPtr.Zero, IntPtr.Zero);
            Win32.SendMessageW(field, Win32.EM_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);
        }
        else
        {
            Win32.SendMessageW(field, Win32.EM_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void SetLive(string line)
    {
        Win32.SetWindowTextW(_live, "");
        AddLive(line);
    }

    /// <summary>A line in Live. It is spoken only while Live has the focus.</summary>
    private void AddLive(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        bool empty = Win32.GetWindowTextLengthW(_live) == 0;
        AppendToField(_live, (empty ? "" : "\r\n") + line.TrimEnd());
        if (Win32.GetFocus() == _live) _speech.Speak(line, false);
    }

    private void FlushLiveLine(bool all)
    {
        string s = _liveLine.ToString();
        int cut = all ? s.Length : s.LastIndexOf('\n') + 1;
        if (cut <= 0) return;
        foreach (var l in s[..cut].Split('\n'))
            if (l.Trim().Length > 0) AddLive(l.TrimEnd('\r'));
        _liveLine.Remove(0, cut);
    }

    // ================================================================== conversation

    private void Post(Action a)
    {
        _uiQueue.Enqueue(a);
        Win32.PostMessageW(_hwnd, WmPump, IntPtr.Zero, IntPtr.Zero);
    }

    private void SendFromInput(bool queueOnly)
    {
        string typed = Win32.GetText(_input).Trim();
        if (typed.Length == 0 && _attachments.Count == 0) { Win32.MessageBeep(0); return; }
        int attached = _attachments.Count;
        string text = WithAttachments(typed);
        Win32.SetWindowTextW(_input, "");
        // Spoken: what was typed, and how many files went with it — not the paths.
        PlaySend();
        if (_settings.SpeakSent)
            _speech.Speak(attached == 0 ? typed : _loc.Format("speak.sentWithAttachments", typed, attached), true);

        if (_busy || _stopping)
        {
            _queue.Add(text);
            AddLive(_loc.Format(queueOnly ? "live.queued" : "live.queuedBusy", _queue.Count));
            return;
        }
        Dispatch(text);
    }

    private void Dispatch(string text)
    {
        if (!EnsureSession()) return;
        CancelResumeTimer();
        AddEntry(_settings.UserName ?? _loc["role.user"], text);
        _busy = true;
        _turnText.Clear();
        _liveLine.Clear();
        SetLive(_loc["live.working"]);
        if (_queue.Count > 0) AddLive(_loc.Format("live.queued", _queue.Count));
        _session!.SendUserMessage(text);
    }

    private string StateKey => _project.TrimEnd('\\').ToUpperInvariant();

    /// <summary>
    /// Fills History with the conversation this window will continue (the remembered session,
    /// else the newest one in the folder), so it does not open empty. Only on "continue".
    /// </summary>
    private void LoadHistory()
    {
        _entries.Clear();
        _entryStarts.Clear();
        Win32.SetWindowTextW(_history, "");
        if (_forceNew || _settings.StartMode.Equals("new", StringComparison.OrdinalIgnoreCase)) return;

        _state.Sessions.TryGetValue(StateKey, out var sessionId);
        string? file = TranscriptReader.FindFile(_project, _lastSessionId ?? sessionId);
        if (file == null) return;

        List<TranscriptReader.Message> messages;
        try { messages = TranscriptReader.Load(file, _loc["history.image"], 500); }
        catch (Exception ex)
        {
            AddEntry(_loc["role.system"], _loc.Format("history.error", ex.Message));
            return;
        }

        string user = _settings.UserName ?? _loc["role.user"];
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            var e = new HistoryEntry(m.Who == TranscriptReader.Speaker.User ? user : _settings.AssistantName, m.Time, m.Text);
            _entries.Add(e);
            _entryStarts.Add(sb.Length);
            sb.Append(Header(e)).Append("\r\n").Append(Crlf(e.Text)).Append("\r\n\r\n");
        }
        // One assignment instead of hundreds of appends; the view starts at the newest message.
        Win32.SetWindowTextW(_history, sb.ToString());
        int len = Win32.GetWindowTextLengthW(_history);
        Win32.SendMessageW(_history, Win32.EM_SETSEL, (IntPtr)len, (IntPtr)len);
        Win32.SendMessageW(_history, Win32.EM_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);
    }

    // ================================================================== restart for a new build

    /// <summary>
    /// Claude creates this file after building a new version. The window restarts only when
    /// idle (answer finished, nothing queued, not stopping), so no answer is cut off.
    /// </summary>
    private static string RestartFlag => Path.Combine(Settings.AppDataFolder, "restart.request");

    private void CheckRestartRequest()
    {
        if (!File.Exists(RestartFlag) || _busy || _stopping || _queue.Count > 0) return;
        try { File.Delete(RestartFlag); } catch { return; }
        string? command = _settings.RestartCommand;
        if (string.IsNullOrWhiteSpace(command) || !File.Exists(command)) return;

        _speech.Speak(_loc["speak.restarting"], false);
        try
        {
            // The launcher waits for this window to close, refreshes the copy and starts it again.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/s /c \"\"{command}\" --restart\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            AddEntry(_loc["role.system"], _loc.Format("history.error", ex.Message));
            return;
        }
        Win32.PostMessageW(_hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void SaveDraft()
    {
        string text = Win32.GetText(_input);
        if (text.Trim().Length > 0) _state.Drafts[StateKey] = text;
        else _state.Drafts.Remove(StateKey);
        _state.Save();
    }

    /// <summary>Puts back what was typed but not sent last time; the caret goes to its end.</summary>
    private void RestoreDraft()
    {
        if (!_state.Drafts.TryGetValue(StateKey, out var draft) || draft.Length == 0) return;
        Win32.SetWindowTextW(_input, draft);
        Win32.SendMessageW(_input, Win32.EM_SETSEL, (IntPtr)draft.Length, (IntPtr)draft.Length);
        Win32.SendMessageW(_input, Win32.EM_SCROLLCARET, IntPtr.Zero, IntPtr.Zero);
    }

    private bool EnsureSession()
    {
        if (_session is { IsRunning: true }) return true;

        _claudeExe ??= ClaudeLocator.Find(_settings.ClaudePath);
        if (_claudeExe == null)
        {
            AddEntry(_loc["role.system"], _loc.Format("error.noClaude", _settings.UserFile));
            return false;
        }

        var args = new List<string>
        {
            "-p",
            "--input-format", "stream-json",
            "--output-format", "stream-json",
            "--verbose",
            "--include-partial-messages",
            // The first release has no permission dialog: whatever the user's Claude Code
            // settings do not allow is refused, and the refusal is reported in History.
            "--permission-prompts", "none",
        };
        if (!string.IsNullOrWhiteSpace(_settings.Model)) { args.Add("--model"); args.Add(_settings.Model!); }
        if (!string.IsNullOrWhiteSpace(_settings.PermissionMode)) { args.Add("--permission-mode"); args.Add(_settings.PermissionMode!); }
        if (!string.IsNullOrWhiteSpace(_settings.Effort)) { args.Add("--effort"); args.Add(_settings.Effort!); }

        // After the first start (or a restart following a crash or a forced stop) the same
        // conversation always continues; only the very first start honours "startMode".
        bool wantNew = _forceNew || (!_startedOnce && _settings.StartMode.Equals("new", StringComparison.OrdinalIgnoreCase));
        string? resumeId = null;
        if (!wantNew)
        {
            resumeId = _lastSessionId;
            if (resumeId == null) _state.Sessions.TryGetValue(StateKey, out resumeId);
        }
        string startNote;
        if (wantNew)
        {
            startNote = _loc["history.new"];
        }
        else if (!string.IsNullOrEmpty(resumeId))
        {
            args.Add("--resume"); args.Add(resumeId);
            startNote = _loc.Format("history.resuming", resumeId);
        }
        else if (ClaudeLocator.HasConversations(_project))
        {
            args.Add("--continue");
            startNote = _loc["history.continuing"];
        }
        else startNote = _loc["history.new"];
        // The window's own note, then the chat's instructions (New chat), on every start.
        string prompt = SystemPromptForWindow;
        var meta = wantNew ? _pendingMeta : resumeId != null && _state.Conversations.TryGetValue(resumeId, out var m) ? m : null;
        if (!string.IsNullOrWhiteSpace(meta?.Instructions)) prompt = (prompt + "\n\n" + meta!.Instructions).Trim();
        if (prompt.Length > 0) { args.Add("--append-system-prompt"); args.Add(prompt); }
        args.AddRange(_settings.ClaudeArgs);

        var session = new ClaudeSession(Post);
        session.Initialized += OnInitialized;
        session.TextDelta += OnTextDelta;
        session.AssistantText += OnAssistantText;
        session.ToolStarted += OnToolStarted;
        session.ToolFinished += OnToolFinished;
        session.Result += OnResult;
        session.Exited += OnExited;
        session.Limits += OnLimits;
        session.Context += OnContext;
        try
        {
            SetLive(_loc["live.starting"]);
            session.Start(_claudeExe, _project, args, _settings.ClaudeEnv);
        }
        catch (Exception ex)
        {
            AddEntry(_loc["role.system"], _loc.Format("error.start", ex.Message));
            SetLive(_loc["live.idle"]);
            return false;
        }

        _session = session;
        _forceNew = false;
        if (!_startedOnce) AddEntry(_loc["role.system"], _loc.Format("history.started", _project, startNote));
        _startedOnce = true;
        return true;
    }

    private void OnInitialized(string sessionId, string model)
    {
        if (sessionId.Length > 0)
        {
            _lastSessionId = sessionId;
            _state.Sessions[StateKey] = sessionId;
            if (_pendingMeta != null) { _state.Conversations[sessionId] = _pendingMeta; _pendingMeta = null; }
            _state.Save();
        }
        AddLive(_loc.Format("live.session", sessionId, model));
    }

    private void OnTextDelta(string text)
    {
        _liveLine.Append(text);
        FlushLiveLine(all: false);
    }

    private void OnAssistantText(string text)
    {
        if (_turnText.Length > 0) _turnText.Append("\n\n");
        _turnText.Append(text.Trim());
    }

    private void OnToolStarted(string name, string summary)
    {
        FlushLiveLine(all: true);
        AddLive(_loc.Format("live.tool", summary.Length > 0 ? $"{name} — {summary}" : name));
    }

    private void OnToolFinished(string name, bool isError) =>
        AddLive(_loc.Format(isError ? "live.toolFailed" : "live.toolDone", name));

    /// <summary>
    /// Moves the answer into History. A complete answer (<paramref name="speak"/>) is also
    /// spoken, unless Live has the focus: there it was already heard while it was written.
    /// </summary>
    private void FlushTurn(bool speak = false)
    {
        if (_turnText.Length == 0) return;
        string text = _turnText.ToString();
        AddEntry(_settings.AssistantName, text);
        _turnText.Clear();
        if (speak) PlayBell();
        if (speak && _settings.SpeakReplies && Win32.GetFocus() != _live) _speech.Speak(text, false);
    }

    private void OnResult(ResultInfo r)
    {
        FlushLiveLine(all: true);
        Win32.KillTimer(_hwnd, (IntPtr)TimerStop);
        bool wasStopping = _stopping;
        _busy = false;
        _stopping = false;
        if (_restartAfterTurn) RestartClaudeWhenIdle(); // a combo changed during the answer
        UpdateTypingSound();

        if (_turnText.Length == 0 && !r.IsError && r.Text.Length > 0) _turnText.Append(r.Text);
        FlushTurn(speak: !wasStopping);

        if (r.Denials.Count > 0)
            AddEntry(_loc["role.system"], string.Join(Environment.NewLine, r.Denials.Select(d => _loc.Format("history.denied", d))));

        if (wasStopping)
        {
            _handoff = Handoff.None; // Stop also calls off a hand-over
            AddEntry(_loc["role.system"], _loc["history.stopped"]);
            SetLive(_loc["live.stopped"]);
            RestoreQueueToInput();
            return;
        }

        if (r.UsageLimit)
        {
            OnUsageLimit(r);
            return;
        }

        _lastLimit = null;
        if (r.IsError)
            AddEntry(_loc["role.system"], _loc.Format("history.error", r.Text.Length > 0 ? r.Text : r.Subtype));
        if (r.IsError && LooksLikeSignInProblem(r.Text))
            AddEntry(_loc["role.system"], _loc["login.hint"]);

        SetLive(_loc["live.idle"]);

        // The hand-over goes before anything queued; queued messages follow in the new conversation.
        if (_handoff == Handoff.Writing && r.IsError) _handoff = Handoff.None; // the error is in History
        if (_handoff == Handoff.Writing) { StartAfterHandoff(); return; }
        if (_handoff == Handoff.Due) { StartHandoffWriting(); return; }

        if (_queue.Count > 0)
        {
            string next = _queue[0];
            _queue.RemoveAt(0);
            Dispatch(next);
            return;
        }
        CheckRestartRequest();
    }

    private void OnExited(int code, string stderr)
    {
        FlushLiveLine(all: true);
        FlushTurn();
        Win32.KillTimer(_hwnd, (IntPtr)TimerStop);
        bool wasBusy = _busy || _stopping;
        _busy = false;
        _stopping = false;
        _session = null;
        if (wasBusy || code != 0)
            AddEntry(_loc["role.system"], _loc.Format("history.exited", code, stderr.Trim()));
        SetLive(_loc.Format("live.exited", code));
    }

    // ================================================================== stop

    private void Stop()
    {
        if (!_busy)
        {
            // Idle: Stop cancels a scheduled resume after the usage limit.
            if (_resumeAt != null) { CancelResumeTimer(); SetLive(_loc["live.stopped"]); }
            else Win32.MessageBeep(0);
            return;
        }
        if (_stopping) return;
        _stopping = true;
        SetLive(_loc["live.stopping"]);
        _session?.Interrupt();
        // If Claude Code does not confirm within five seconds, end the process; the next
        // message resumes the same conversation.
        Win32.SetTimer(_hwnd, (IntPtr)TimerStop, 5000, IntPtr.Zero);
    }

    private void ForceStop()
    {
        Win32.KillTimer(_hwnd, (IntPtr)TimerStop);
        if (!_stopping) return;
        _session?.Kill();
        _session = null;
        _busy = false;
        _stopping = false;
        FlushLiveLine(all: true);
        FlushTurn();
        _handoff = Handoff.None;
        AddEntry(_loc["role.system"], _loc["history.stopped"]);
        SetLive(_loc["live.stopped"]);
        RestoreQueueToInput();
    }

    /// <summary>Nothing typed is lost: unsent queued messages go back into the Message field.</summary>
    private void RestoreQueueToInput()
    {
        if (_queue.Count == 0) return;
        string current = Win32.GetText(_input);
        var parts = new List<string>(_queue);
        if (current.Trim().Length > 0) parts.Add(current);
        _queue.Clear();
        Win32.SetWindowTextW(_input, Crlf(string.Join("\n\n", parts)));
        AddEntry(_loc["role.system"], _loc["history.restoredQueue"]);
    }

    // ================================================================== usage limit

    private bool ResumeChecked => _settings.ResumeAfterLimit;

    /// <summary>Session → Resume after usage limit: a checked menu item, remembered.</summary>
    private void OnResumeToggled()
    {
        bool on = !_settings.ResumeAfterLimit;
        _settings.ResumeAfterLimit = on;
        _settings.SaveUserValue("resumeAfterLimit", on);
        BuildMenu();
        if (!on) { CancelResumeTimer(); if (_lastLimit != null) SetLive(_loc["live.limitNoResume"]); }
        else if (_lastLimit != null && !_busy) ScheduleResume(_lastLimit);
    }

    private void OnUsageLimit(ResultInfo r)
    {
        _lastLimit = r;
        AddEntry(_loc["role.system"], _loc.Format("history.limit", r.Text));
        if (ResumeChecked) ScheduleResume(r);
        else SetLive(_loc["live.limitNoResume"]);
    }

    private void ScheduleResume(ResultInfo r)
    {
        if (r.ResetsAt is { } at && at > DateTimeOffset.Now)
        {
            _resumeAt = at.AddSeconds(Math.Max(0, _settings.LimitResumeDelaySeconds));
            SetLive(_loc.Format("live.resumeAt", _resumeAt.Value.ToLocalTime().ToString("t", _loc.Culture)));
        }
        else
        {
            int minutes = Math.Max(1, _settings.LimitRetryMinutes);
            _resumeAt = DateTimeOffset.Now.AddMinutes(minutes);
            SetLive(_loc.Format("live.retryIn", minutes));
        }
        Win32.SetTimer(_hwnd, (IntPtr)TimerLimit, 30_000, IntPtr.Zero);
    }

    private void CancelResumeTimer()
    {
        _resumeAt = null;
        Win32.KillTimer(_hwnd, (IntPtr)TimerLimit);
    }

    private void OnTimer(int id)
    {
        if (id == TimerStop) { ForceStop(); return; }
        if (id == TimerRestart) { CheckRestartRequest(); return; }
        if (id == TimerSound) { UpdateTypingSound(); return; }
        if (id != TimerLimit) return;
        if (!ResumeChecked || _resumeAt == null) { CancelResumeTimer(); return; }
        if (DateTimeOffset.Now < _resumeAt.Value || _busy) return;

        CancelResumeTimer();
        AddEntry(_loc["role.system"], _loc["history.resumed"]);
        Dispatch(string.IsNullOrWhiteSpace(_settings.ResumePrompt) ? _loc["resume.prompt"] : _settings.ResumePrompt!);
    }

    // ================================================================== project / conversation

    private void NewConversation() => SwitchTo(_project, null, newConversation: true, null);

    private void OpenProject()
    {
        string? folder = Program.PickFolder(_hwnd, _loc["dialog.pickFolder"], _project);
        if (folder == null || !Directory.Exists(folder)) return;
        SwitchTo(folder, null, newConversation: false, null);
    }

    // ================================================================== File → Chat / Code

    private string ProjectsRoot =>
        !string.IsNullOrWhiteSpace(_settings.ProjectsRoot) ? _settings.ProjectsRoot!
        : Path.GetDirectoryName(_project.TrimEnd('\\')) ?? _project;

    private string ChatFolderPath => Path.Combine(ProjectsRoot, _settings.ChatFolder);

    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private string? CurrentSessionId()
    {
        if (_lastSessionId != null) return _lastSessionId;
        return _forceNew ? null : _state.Sessions.TryGetValue(StateKey, out var id) ? id : null;
    }

    private uint Target(string folder, string? sessionId, bool isNew, string label)
    {
        _menuTargets.Add(new MenuTarget(folder, sessionId, isNew, label));
        return (uint)(IdmTargetBase + _menuTargets.Count - 1);
    }

    /// <summary>"New conversation", then the folder's conversations, newest first, numbered 1–9.</summary>
    private IntPtr BuildConversationList(string folder, int max)
    {
        IntPtr popup = Win32.CreatePopupMenu();
        // In Chat the wizard is the only way in (author, 2026-11-07): confirmed empty, it is a plain new chat.
        if (SameFolder(folder, ChatFolderPath)) Win32.AppendMenuW(popup, Win32.MF_STRING, IdmNewChat, _loc["menu.file.newChat"]);
        else Win32.AppendMenuW(popup, Win32.MF_STRING, (IntPtr)Target(folder, null, true, _loc["conv.new"]), _loc["menu.conv.new"]);
        Win32.AppendMenuW(popup, Win32.MF_SEPARATOR, IntPtr.Zero, null);

        var sessions = OrderedSessions(folder, max);
        if (sessions.Count == 0)
        {
            Win32.AppendMenuW(popup, Win32.MF_STRING | Win32.MF_GRAYED, IntPtr.Zero, _loc["menu.conv.none"]);
            return popup;
        }

        string? current = SameFolder(folder, _project) ? CurrentSessionId() : null;
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            string when = s.When.ToString("g", _loc.Culture);
            string label = ConversationLabel(s);
            string text = (i < 9 ? $"&{i + 1} " : $"{i + 1} ") + label.Replace("&", "&&") + " — " + when;
            uint flags = Win32.MF_STRING | (s.Id == current ? Win32.MF_CHECKED : 0);
            Win32.AppendMenuW(popup, flags, (IntPtr)Target(folder, s.Id, false, s.Label), text);
        }
        return popup;
    }

    private IntPtr BuildChatMenu() => BuildConversationList(ChatFolderPath, 15);

    /// <summary>One submenu per project folder (every subfolder of the root except Chat).</summary>
    private IntPtr BuildCodeMenu()
    {
        IntPtr code = Win32.CreatePopupMenu();
        Win32.AppendMenuW(code, Win32.MF_STRING, IdmNewProject, _loc["menu.file.newProject"]);
        Win32.AppendMenuW(code, Win32.MF_STRING, IdmOpenExisting, _loc["menu.file.openExisting"]);
        Win32.AppendMenuW(code, Win32.MF_SEPARATOR, IntPtr.Zero, null);
        var projects = ProjectFolders();

        if (projects.Count == 0)
            Win32.AppendMenuW(code, Win32.MF_STRING | Win32.MF_GRAYED, IntPtr.Zero, _loc["menu.conv.none"]);
        foreach (var p in projects)
        {
            uint flags = Win32.MF_POPUP | (SameFolder(p, _project) ? Win32.MF_CHECKED : 0);
            Win32.AppendMenuW(code, flags, BuildConversationList(p, 10), ProjectLabel(p).Replace("&", "&&"));
        }
        return code;
    }

    private void RefreshConversationMenus()
    {
        _menuTargets.Clear();
        // ModifyMenu destroys the old submenus it replaces.
        Win32.ModifyMenuW(_fileMenu, 0, Win32.MF_BYPOSITION | Win32.MF_POPUP, BuildChatMenu(), _loc["menu.file.chat"]);
        Win32.ModifyMenuW(_fileMenu, 1, Win32.MF_BYPOSITION | Win32.MF_POPUP, BuildCodeMenu(), _loc["menu.file.code"]);
    }

    /// <summary>
    /// Moves the window to another folder and conversation: a given session is resumed, a new one
    /// starts empty, and with neither the folder's last conversation continues. Not while answering.
    /// </summary>
    private void SwitchTo(string folder, string? sessionId, bool newConversation, string? label, ConversationMeta? pending = null)
    {
        if (_busy || _stopping)
        {
            Win32.MessageBeep(0);
            AddLive(_loc["live.busySwitch"]);
            return;
        }
        try { Directory.CreateDirectory(folder); } catch { }

        SaveDraft();
        _session?.Kill();
        _session = null;
        CancelResumeTimer();
        _lastLimit = null;

        _handoffWarned = false;
        _ctxPercent = 0;
        bool sameProject = SameFolder(folder, _project);
        _ctxText = "—"; // the context belongs to the conversation; limits are the account's
        _project = Path.GetFullPath(folder).TrimEnd('\\');
        if (_project.EndsWith(':')) _project += "\\";
        // Read again on every switch: a chat's own model and effort must not stick to the next one.
        _settings = Settings.Load(_userFile, _project, _cli);
        if (!sameProject) _claudeExe = ClaudeLocator.Find(_settings.ClaudePath);
        _pendingMeta = newConversation ? pending : null;

        _startedOnce = false;
        _forceNew = newConversation;
        _lastSessionId = newConversation ? null : sessionId;
        if (newConversation) _state.Sessions.Remove(StateKey);
        else if (sessionId != null) _state.Sessions[StateKey] = sessionId;
        if (!UnderRoot(_project) && !SameFolder(_project, ChatFolderPath) && !_state.ExtraProjects.Any(e => SameFolder(e, _project)))
            _state.ExtraProjects.Add(_project);
        _state.HiddenProjects.RemoveAll(h => SameFolder(h, _project));
        _state.LastProject = _project;
        _state.Save();

        ApplyTexts();
        LoadHistory();
        Win32.SetWindowTextW(_input, "");
        RestoreDraft();
        SetLive(_loc["live.idle"]);
        Win32.SetFocus(_input);

        string what = newConversation ? _loc["conv.new"] : label ?? _loc["conv.last"];
        _speech.Speak(_loc.Format("speak.switched", Path.GetFileName(_project.TrimEnd('\\')), what), true);
    }
}
