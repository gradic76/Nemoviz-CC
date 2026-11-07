using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// A small modal form for the wizards (author, 2026-11-07): rows on the left — a label to the left
/// of its field, combo or list, check boxes across — and on the right one large field with its
/// label, the buttons below it. Creation order is the Tab order: left rows top to bottom, the
/// large field, the buttons. Enter presses the default button (id 1), Escape the cancel one (id 2).
/// After Alt+Tab the focus goes back where it was.
/// </summary>
internal sealed partial class MainWindow
{
    private const string FormClass = "ClaudeChat.Form";
    private static readonly WndProcDelegate s_formProc = FormProcStatic;
    private static bool s_formClassRegistered;

    private const int FormOk = 1, FormCancel = 2, FormSaveLater = 3, FormBack = 4;

    private sealed class Form
    {
        public IntPtr Hwnd;
        public readonly List<(IntPtr Label, IntPtr Control, int Lines)> Rows = new();
        public IntPtr BigLabel, Big;
        public readonly List<IntPtr> Buttons = new();
        public int Result = FormCancel;
        public bool Done;
        public IntPtr LastFocus;
        /// <summary>Any WM_COMMAND (id, notification code). Return true to keep the form open
        /// when a bottom button was pressed (e.g. a failed check).</summary>
        public Func<int, int, bool>? OnCommand;
        /// <summary>Called with the result while the controls still exist: read the values here.</summary>
        public Action<int>? BeforeClose;
        public int NextId = 3000;
    }

    private Form? _form;

    private Form BeginForm(string title)
    {
        if (!s_formClassRegistered) { RegisterClass(FormClass, s_formProc); s_formClassRegistered = true; }
        var f = new Form();
        f.Hwnd = Win32.CreateWindowExW(Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_CONTROLPARENT, FormClass, title,
            Win32.WS_POPUP | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_CLIPCHILDREN, 0, 0, S(800), S(600), _hwnd, IntPtr.Zero, _hInst, IntPtr.Zero);
        return f;
    }

    private IntPtr FormChild(Form f, string cls, string text, uint style, uint ex, int id = 0)
    {
        if (id == 0) id = f.NextId++;
        IntPtr h = Win32.CreateWindowExW(ex, cls, text, Win32.WS_CHILD | Win32.WS_VISIBLE | style, 0, 0, 0, 0, f.Hwnd, (IntPtr)id, _hInst, IntPtr.Zero);
        Win32.SendMessageW(h, Win32.WM_SETFONT, _font, (IntPtr)1);
        return h;
    }

    private IntPtr FormLabel(Form f, string text) => FormChild(f, "STATIC", text, Win32.SS_LEFT | Win32.SS_NOPREFIX, 0);

    private IntPtr FEdit(Form f, string label, string text)
    {
        IntPtr l = FormLabel(f, label);
        IntPtr e = FormChild(f, "EDIT", text, Win32.ES_AUTOHSCROLL | Win32.WS_TABSTOP, Win32.WS_EX_CLIENTEDGE);
        f.Rows.Add((l, e, 1));
        return e;
    }

    private IntPtr FCombo(Form f, string label, IEnumerable<string> items, int selected, int id = 0)
    {
        IntPtr l = FormLabel(f, label);
        IntPtr c = FormChild(f, "COMBOBOX", "", Win32.CBS_DROPDOWNLIST | Win32.WS_VSCROLL | Win32.WS_TABSTOP, 0, id);
        Fill(c, items, selected);
        f.Rows.Add((l, c, 1));
        return c;
    }

    private IntPtr FCheck(Form f, string text, bool on)
    {
        IntPtr c = FormChild(f, "BUTTON", text, Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP, 0);
        Check(c, on);
        f.Rows.Add((IntPtr.Zero, c, 1));
        return c;
    }

    private IntPtr FList(Form f, string label, IEnumerable<string> items, int lines, int id = 0)
    {
        IntPtr l = FormLabel(f, label);
        IntPtr c = FormChild(f, "LISTBOX", "", 0x0001 /* LBS_NOTIFY */ | 0x0100 /* LBS_NOINTEGRALHEIGHT */ | Win32.WS_VSCROLL | Win32.WS_TABSTOP, Win32.WS_EX_CLIENTEDGE, id);
        foreach (var it in items) Win32.SendMessageW(c, 0x0180 /* LB_ADDSTRING */, IntPtr.Zero, it);
        if (ListCount(c) > 0) Win32.SendMessageW(c, 0x0186 /* LB_SETCURSEL */, IntPtr.Zero, IntPtr.Zero);
        f.Rows.Add((l, c, lines));
        return c;
    }

    /// <summary>A row of push buttons in the left column (under a list, for example).</summary>
    private void FButtonRow(Form f, params (int Id, string Text)[] buttons)
    {
        foreach (var (id, text) in buttons)
            f.Rows.Add((IntPtr.Zero, FormChild(f, "BUTTON", text, Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 0, id), -1));
    }

    private IntPtr FBig(Form f, string label, string text, bool readOnly)
    {
        f.BigLabel = FormLabel(f, label);
        uint style = Win32.ES_MULTILINE | Win32.ES_AUTOVSCROLL | Win32.WS_VSCROLL | Win32.WS_TABSTOP | (readOnly ? Win32.ES_READONLY : Win32.ES_WANTRETURN);
        f.Big = FormChild(f, "EDIT", ToEdit(text), style, Win32.WS_EX_CLIENTEDGE);
        Win32.SendMessageW(f.Big, Win32.EM_SETLIMITTEXT, IntPtr.Zero, IntPtr.Zero);
        return f.Big;
    }

    private void FButton(Form f, int id, string text, bool isDefault = false) =>
        f.Buttons.Add(FormChild(f, "BUTTON", text, (isDefault ? Win32.BS_DEFPUSHBUTTON : Win32.BS_PUSHBUTTON) | Win32.WS_TABSTOP, 0, id));

    private static int ListCount(IntPtr list) => (int)Win32.SendMessageW(list, 0x018B /* LB_GETCOUNT */, IntPtr.Zero, IntPtr.Zero);

    private static List<string> ListItems(IntPtr list)
    {
        var items = new List<string>();
        int n = ListCount(list);
        for (int i = 0; i < n; i++)
        {
            int len = (int)Win32.SendMessageW(list, 0x018A /* LB_GETTEXTLEN */, (IntPtr)i, IntPtr.Zero);
            var sb = new System.Text.StringBuilder(len + 1);
            LbGetText(list, 0x0189 /* LB_GETTEXT */, (IntPtr)i, sb);
            items.Add(sb.ToString());
        }
        return items;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr LbGetText(IntPtr h, uint msg, IntPtr w, System.Text.StringBuilder s);

    private static void ListAdd(IntPtr list, IEnumerable<string> items)
    {
        var have = new HashSet<string>(ListItems(list), StringComparer.OrdinalIgnoreCase);
        foreach (var it in items)
            if (have.Add(it)) Win32.SendMessageW(list, 0x0180 /* LB_ADDSTRING */, IntPtr.Zero, it);
        Win32.SendMessageW(list, 0x0186 /* LB_SETCURSEL */, (IntPtr)(ListCount(list) - 1), IntPtr.Zero);
    }

    private static void ListRemoveSelected(IntPtr list)
    {
        int i = (int)Win32.SendMessageW(list, 0x0188 /* LB_GETCURSEL */, IntPtr.Zero, IntPtr.Zero);
        if (i < 0) { Win32.MessageBeep(0); return; }
        Win32.SendMessageW(list, 0x0182 /* LB_DELETESTRING */, (IntPtr)i, IntPtr.Zero);
        int n = ListCount(list);
        if (n > 0) Win32.SendMessageW(list, 0x0186 /* LB_SETCURSEL */, (IntPtr)Math.Min(i, n - 1), IntPtr.Zero);
    }

    private void LayoutForm(Form f)
    {
        int pad = S(10), gap = S(6), lineH = Measure("Ag").cy, rowH = lineH + S(12);
        // The left column: labels as wide as the widest one, fields after them.
        int labelW = 0;
        foreach (var r in f.Rows) if (r.Label != IntPtr.Zero) labelW = Math.Max(labelW, Measure(Win32.GetText(r.Label)).cx);
        labelW += S(8);
        int fieldW = S(300);
        int leftW = labelW + fieldW;
        int y = pad;
        int i = 0;
        while (i < f.Rows.Count)
        {
            var r = f.Rows[i];
            if (r.Lines == -1)
            {
                // Consecutive buttons share one row, under the fields.
                int x = pad + labelW;
                while (i < f.Rows.Count && f.Rows[i].Lines == -1)
                {
                    var b = f.Rows[i].Control;
                    int bw = Math.Max(S(90), Measure(Win32.GetText(b)).cx + S(24));
                    Move(b, x, y, bw, rowH);
                    x += bw + gap;
                    i++;
                }
                y += rowH + gap;
                continue;
            }
            int h = r.Lines > 1 ? lineH * r.Lines + S(8) : rowH;
            var cls = new System.Text.StringBuilder(16);
            GetClassNameW(r.Control, cls, 16);
            bool combo = cls.ToString() == "ComboBox";
            if (r.Label == IntPtr.Zero) Move(r.Control, pad, y, leftW, h);
            else
            {
                Move(r.Label, pad, y + S(4), labelW - S(6), lineH + S(4));
                Move(r.Control, pad + labelW, y, fieldW, combo ? rowH + S(200) : h);
            }
            y += h + gap;
            i++;
        }
        int leftBottom = y;

        int bigX = pad + leftW + pad * 2, bigW = S(380);
        int btnW = f.Buttons.Count == 0 ? 0 : f.Buttons.Max(b => Math.Max(S(90), Measure(Win32.GetText(b)).cx + S(24)));
        bigW = Math.Max(bigW, f.Buttons.Count * (btnW + gap) - gap);
        int clientW = bigX + bigW + pad;
        int clientH = Math.Max(leftBottom, S(330)) + pad;

        // Fit the owner window's height (the main window is as tall as the work area).
        Win32.GetWindowRect(_hwnd, out RECT owner);
        Win32.GetWindowRect(f.Hwnd, out RECT wr);
        Win32.GetClientRect(f.Hwnd, out RECT cr);
        int frameW = wr.Width - cr.Width, frameH = wr.Height - cr.Height;
        clientH = Math.Min(clientH, owner.Height - frameH);
        int winW = clientW + frameW, winH = clientH + frameH;
        int left = owner.Right - winW; // beside the right edge, where the main window is
        Win32.SetWindowPos(f.Hwnd, IntPtr.Zero, Math.Max(0, left), owner.Top + S(30), winW, winH, 0x0004 | 0x0010 /* NOZORDER|NOACTIVATE */);

        int by = clientH - pad - rowH;
        if (f.Big != IntPtr.Zero)
        {
            Move(f.BigLabel, bigX, pad, bigW, lineH + S(4));
            Move(f.Big, bigX, pad + lineH + S(6), bigW, by - gap - (pad + lineH + S(6)));
        }
        int bx = bigX + bigW;
        for (int b = f.Buttons.Count - 1; b >= 0; b--)
        {
            bx -= btnW;
            Move(f.Buttons[b], bx, by, btnW, rowH);
            bx -= gap;
        }
    }

    /// <summary>Shows the form modally; returns the id of the button that closed it.</summary>
    private int RunForm(Form f, IntPtr focus)
    {
        LayoutForm(f);
        foreach (var c in AllChildren(f)) ThemeDialogControl(c);
        _theme.ApplyToTopLevel(f.Hwnd);
        _form = f;
        IntPtr focusBefore = Win32.GetFocus();
        Win32.EnableWindow(_hwnd, false);
        Win32.ShowWindow(f.Hwnd, Win32.SW_SHOWNORMAL);
        Win32.SetFocus(focus != IntPtr.Zero ? focus : f.Rows.Count > 0 ? f.Rows[0].Control : f.Big);

        while (!f.Done)
        {
            if (Win32.GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) <= 0) { Win32.PostQuitMessage(0); break; }
            // The large field wants every key, Tab too; here Tab moves on, Enter makes a new line.
            if (msg.message == Win32.WM_KEYDOWN && (int)msg.wParam == Win32.VK_TAB && msg.hwnd == f.Big && !Win32.KeyDown(Win32.VK_CONTROL))
            {
                IntPtr next = Win32.GetNextDlgTabItem(f.Hwnd, msg.hwnd, Win32.KeyDown(Win32.VK_SHIFT));
                if (next != IntPtr.Zero) Win32.SetFocus(next);
                continue;
            }
            if (Win32.IsDialogMessageW(f.Hwnd, ref msg)) continue;
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }

        f.BeforeClose?.Invoke(f.Result);
        Win32.EnableWindow(_hwnd, true);
        Win32.DestroyWindow(f.Hwnd);
        _form = null;
        Win32.SetForegroundWindow(_hwnd);
        Win32.SetFocus(focusBefore != IntPtr.Zero && Win32.IsWindow(focusBefore) ? focusBefore : _input);
        return f.Result;
    }

    private static IEnumerable<IntPtr> AllChildren(Form f)
    {
        foreach (var r in f.Rows) { if (r.Label != IntPtr.Zero) yield return r.Label; yield return r.Control; }
        if (f.Big != IntPtr.Zero) { yield return f.BigLabel; yield return f.Big; }
        foreach (var b in f.Buttons) yield return b;
    }

    private static IntPtr FormProcStatic(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var self = s_instance;
        var f = self?._form;
        if (self != null && f != null && hwnd == f.Hwnd)
        {
            switch (msg)
            {
                case Win32.WM_COMMAND:
                {
                    int id = Win32.LoWord(wParam), code = (int)((long)wParam >> 16) & 0xFFFF;
                    bool bottom = id is FormOk or FormCancel or FormSaveLater or FormBack;
                    bool keepOpen = f.OnCommand?.Invoke(id, code) ?? false;
                    if (bottom && !(keepOpen && id != FormCancel)) { f.Result = id; f.Done = true; }
                    return IntPtr.Zero;
                }
                case Win32.WM_CLOSE:
                    f.Result = FormCancel; f.Done = true;
                    return IntPtr.Zero;
                case 0x0006: // WM_ACTIVATE: back to the last control after Alt+Tab
                    if (Win32.LoWord(wParam) == 0)
                    {
                        IntPtr fo = Win32.GetFocus();
                        if (fo != IntPtr.Zero && Win32.IsChild(hwnd, fo)) f.LastFocus = fo;
                    }
                    else if (f.LastFocus != IntPtr.Zero && Win32.IsWindow(f.LastFocus))
                    {
                        Win32.SetFocus(f.LastFocus);
                        return IntPtr.Zero;
                    }
                    break;
                case Win32.WM_ERASEBKGND:
                    Win32.GetClientRect(hwnd, out RECT rc);
                    Win32.FillRect(wParam, ref rc, self._theme.WindowBrush);
                    return (IntPtr)1;
                case Win32.WM_CTLCOLORSTATIC:
                    if (lParam == f.Big) // read-only large field
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
