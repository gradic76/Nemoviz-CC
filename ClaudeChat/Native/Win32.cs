using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeChat.Native;

internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASSEX
{
    public uint cbSize;
    public uint style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    public string? lpszMenuName;
    public string lpszClassName;
    public IntPtr hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE { public int cx, cy; }

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct LOGFONT
{
    public int lfHeight;
    public int lfWidth;
    public int lfEscapement;
    public int lfOrientation;
    public int lfWeight;
    public byte lfItalic;
    public byte lfUnderline;
    public byte lfStrikeOut;
    public byte lfCharSet;
    public byte lfOutPrecision;
    public byte lfClipPrecision;
    public byte lfQuality;
    public byte lfPitchAndFamily;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string lfFaceName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NONCLIENTMETRICS
{
    public uint cbSize;
    public int iBorderWidth;
    public int iScrollWidth;
    public int iScrollHeight;
    public int iCaptionWidth;
    public int iCaptionHeight;
    public LOGFONT lfCaptionFont;
    public int iSmCaptionWidth;
    public int iSmCaptionHeight;
    public LOGFONT lfSmCaptionFont;
    public int iMenuWidth;
    public int iMenuHeight;
    public LOGFONT lfMenuFont;
    public LOGFONT lfStatusFont;
    public LOGFONT lfMessageFont;
    public int iPaddedBorderWidth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HIGHCONTRAST
{
    public uint cbSize;
    public uint dwFlags;
    public IntPtr lpszDefaultScheme;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INITCOMMONCONTROLSEX
{
    public uint dwSize;
    public uint dwICC;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct BROWSEINFO
{
    public IntPtr hwndOwner;
    public IntPtr pidlRoot;
    public IntPtr pszDisplayName;
    public string lpszTitle;
    public uint ulFlags;
    public IntPtr lpfn;
    public IntPtr lParam;
    public int iImage;
}

internal delegate IntPtr SubclassProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr refData);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OPENFILENAME
{
    public int lStructSize;
    public IntPtr hwndOwner;
    public IntPtr hInstance;
    public string? lpstrFilter;
    public IntPtr lpstrCustomFilter;
    public int nMaxCustFilter;
    public int nFilterIndex;
    public IntPtr lpstrFile;
    public int nMaxFile;
    public IntPtr lpstrFileTitle;
    public int nMaxFileTitle;
    public string? lpstrInitialDir;
    public string? lpstrTitle;
    public int Flags;
    public short nFileOffset;
    public short nFileExtension;
    public string? lpstrDefExt;
    public IntPtr lCustData;
    public IntPtr lpfnHook;
    public IntPtr lpTemplateName;
    public IntPtr pvReserved;
    public int dwReserved;
    public int FlagsEx;
}

internal static class Win32
{
    // Context menu, clipboard, Save As
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint EM_POSFROMCHAR = 0x00D6;
    public const uint EM_CHARFROMPOS = 0x00D7;
    public const uint EM_LINEINDEX = 0x00BB;
    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint CF_UNICODETEXT = 13;
    public const uint GMEM_MOVEABLE = 0x0002;
    public const int OFN_OVERWRITEPROMPT = 0x00000002;
    public const int OFN_PATHMUSTEXIST = 0x00000800;
    public const int OFN_EXPLORER = 0x00080000;

    // Tab control
    public const uint TCM_INSERTITEMW = 0x133E;
    public const uint TCM_GETCURSEL = 0x130B;
    public const uint TCM_SETCURSEL = 0x130C;
    public const int TCN_SELCHANGE = -551;
    public const uint WM_NOTIFY = 0x004E;
    public const uint BS_DEFPUSHBUTTON = 0x0001;
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_EX_DLGMODALFRAME = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TCITEM
    {
        public uint mask, dwState, dwStateMask;
        public string pszText;
        public int cchTextMax, iImage;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMHDR { public IntPtr hwndFrom; public UIntPtr idFrom; public int code; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, ref TCITEM item);

    [DllImport("user32.dll")] public static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hWnd, IntPtr tpm);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool CloseClipboard();
    [DllImport("user32.dll")] public static extern bool EmptyClipboard();
    [DllImport("user32.dll")] public static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr mem);
    [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr mem);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalFree(IntPtr mem);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] public static extern bool GetSaveFileNameW(ref OPENFILENAME ofn);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] public static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);
    public const int OFN_ALLOWMULTISELECT = 0x00000200;
    public const int OFN_FILEMUSTEXIST = 0x00001000;

    // Edit commands
    public const uint EM_CANUNDO = 0x00C6;
    public const uint EM_UNDO = 0x00C7;
    public const uint WM_CUT = 0x0300;
    public const uint WM_COPY = 0x0301;
    public const uint WM_PASTE = 0x0302;
    public const uint WM_CLEAR = 0x0303;
    public const uint WM_DROPFILES = 0x0233;

    // Clipboard reading
    public const uint CF_DIB = 8;
    public const uint CF_HDROP = 15;
    [DllImport("user32.dll")] public static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] public static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll")] public static extern UIntPtr GlobalSize(IntPtr mem);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern uint DragQueryFileW(IntPtr drop, uint index, StringBuilder? file, uint size);
    [DllImport("shell32.dll")] public static extern void DragFinish(IntPtr drop);
    [DllImport("shell32.dll")] public static extern void DragAcceptFiles(IntPtr hWnd, bool accept);
    [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProcDelegate proc, UIntPtr id, IntPtr refData);
    [DllImport("comctl32.dll")] public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Puts Unicode text on the clipboard. Returns false if another program holds it.</summary>
    public static bool SetClipboardText(IntPtr owner, string text)
    {
        if (!OpenClipboard(owner)) return false;
        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (mem == IntPtr.Zero) return false;
            IntPtr p = GlobalLock(mem);
            Marshal.Copy(text.ToCharArray(), 0, p, text.Length);
            Marshal.WriteInt16(p, text.Length * 2, 0);
            GlobalUnlock(mem);
            if (SetClipboardData(CF_UNICODETEXT, mem) == IntPtr.Zero) { GlobalFree(mem); return false; }
            return true; // the clipboard owns the memory now
        }
        finally { CloseClipboard(); }
    }

    // Window styles
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const uint WS_TABSTOP = 0x00010000;
    public const uint WS_GROUP = 0x00020000;
    public const uint WS_VSCROLL = 0x00200000;
    public const uint WS_BORDER = 0x00800000;
    public const uint WS_EX_CLIENTEDGE = 0x00000200;
    public const uint WS_EX_CONTROLPARENT = 0x00010000;
    public const uint WS_EX_LAYOUTRTL = 0x00400000;

    // Edit styles
    public const uint ES_MULTILINE = 0x0004;
    public const uint ES_AUTOVSCROLL = 0x0040;
    public const uint ES_NOHIDESEL = 0x0100;
    public const uint ES_READONLY = 0x0800;
    public const uint ES_WANTRETURN = 0x1000;

    public const uint ES_AUTOHSCROLL = 0x0080;
    // Combo box
    public const uint CBS_DROPDOWNLIST = 0x0003;
    public const uint CB_ADDSTRING = 0x0143;
    public const uint CB_GETCURSEL = 0x0147;
    public const uint CB_RESETCONTENT = 0x014B;
    public const uint CB_SETCURSEL = 0x014E;
    public const int CBN_SELCHANGE = 1;
    public const uint WM_CTLCOLORLISTBOX = 0x0134;

    // Button styles
    public const uint BS_PUSHBUTTON = 0x0000;
    public const uint BS_AUTOCHECKBOX = 0x0003;
    public const uint BS_MULTILINE = 0x2000;

    // Static styles
    public const uint SS_LEFT = 0x0000;
    public const uint SS_NOPREFIX = 0x0080;

    // Messages
    public const uint WM_CREATE = 0x0001;
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_SETFOCUS = 0x0007;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_SYSCOLORCHANGE = 0x0015;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_GETMINMAXINFO = 0x0024;
    public const uint WM_SETFONT = 0x0030;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_CHAR = 0x0102;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSCHAR = 0x0106;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_CTLCOLOREDIT = 0x0133;
    public const uint WM_CTLCOLORBTN = 0x0135;
    public const uint WM_CTLCOLORSTATIC = 0x0138;
    public const uint WM_DISPLAYCHANGE = 0x007E;
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_THEMECHANGED = 0x031A;
    public const uint WM_APP = 0x8000;

    public const uint EM_GETSEL = 0x00B0;
    public const uint EM_SETSEL = 0x00B1;
    public const uint EM_SCROLLCARET = 0x00B7;
    public const uint EM_REPLACESEL = 0x00C2;
    public const uint EM_SETLIMITTEXT = 0x00C5;
    public const uint EM_GETFIRSTVISIBLELINE = 0x00CE;
    public const uint EM_LINESCROLL = 0x00B6;
    public const uint BM_GETCHECK = 0x00F0;
    public const uint BM_SETCHECK = 0x00F1;
    public const int BST_CHECKED = 1;

    public const int BN_CLICKED = 0;

    // ShowWindow
    public const int SW_SHOW = 5;
    public const int SW_SHOWNORMAL = 1;

    // SetWindowPos
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    // Menus
    public const uint MF_STRING = 0x0000;
    public const uint MF_POPUP = 0x0010;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint MF_BYCOMMAND = 0x0000;
    public const uint MF_CHECKED = 0x0008;
    public const uint MF_UNCHECKED = 0x0000;

    // Virtual keys
    public const int VK_TAB = 0x09;
    public const int VK_RETURN = 0x0D;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_F6 = 0x75;

    public const int GA_ROOT = 2;
    public const int IDC_ARROW = 32512;
    public const int IDI_APPLICATION = 32512;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const uint SPI_GETNONCLIENTMETRICS = 0x0029;
    public const uint SPI_GETHIGHCONTRAST = 0x0042;
    public const uint HCF_HIGHCONTRASTON = 0x0001;
    public const int TRANSPARENT = 1;
    public const uint RDW_INVALIDATE = 0x0001;
    public const uint RDW_ERASE = 0x0004;
    public const uint RDW_ALLCHILDREN = 0x0080;
    public const uint RDW_FRAME = 0x0400;
    public const uint MB_OK = 0x0;
    public const uint MB_ICONINFORMATION = 0x40;
    public const uint MB_ICONERROR = 0x10;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public const int COLOR_WINDOW = 5;
    public const int COLOR_WINDOWTEXT = 8;
    public const int COLOR_BTNFACE = 15;
    public const int COLOR_BTNTEXT = 18;
    public const int COLOR_GRAYTEXT = 17;

    public const uint BIF_RETURNONLYFSDIRS = 0x0001;
    public const uint BIF_NEWDIALOGSTYLE = 0x0040;
    public const uint COINIT_APARTMENTTHREADED = 0x2;
    public const uint ICC_STANDARD_CLASSES = 0x4000;
    public const uint ICC_WIN95_CLASSES = 0x00FF;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] public static extern bool IsDialogMessageW(IntPtr hDlg, ref MSG msg);
    [DllImport("user32.dll")] public static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, out int wParam, out int lParam);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetFocus();
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, int flags);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetNextDlgTabItem(IntPtr hDlg, IntPtr hCtl, bool previous);
    public const uint WM_CHANGEUISTATE = 0x0127;
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool EnableWindow(IntPtr hWnd, bool enable);
    [DllImport("user32.dll")] public static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLengthW(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SetWindowTextW(IntPtr hWnd, string text);
    [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] public static extern bool MessageBeep(uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    [DllImport("user32.dll")] public static extern IntPtr LoadCursorW(IntPtr inst, IntPtr name);
    [DllImport("user32.dll")] public static extern IntPtr LoadIconW(IntPtr inst, IntPtr name);
    [DllImport("user32.dll")] public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr id, uint ms, IntPtr proc);
    [DllImport("user32.dll")] public static extern bool KillTimer(IntPtr hWnd, IntPtr id);
    [DllImport("user32.dll")] public static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);
    [DllImport("user32.dll")] public static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr rgn, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("user32.dll")] public static extern uint GetSysColor(int index);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfoForDpi(uint action, uint param, ref NONCLIENTMETRICS ncm, uint winIni, uint dpi);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfoW(uint action, uint param, ref HIGHCONTRAST hc, uint winIni);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfoW(IntPtr hMon, ref MONITORINFO info);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")] public static extern IntPtr CreateMenu();
    [DllImport("user32.dll")] public static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool AppendMenuW(IntPtr menu, uint flags, IntPtr id, string? text);
    [DllImport("user32.dll")] public static extern bool SetMenu(IntPtr hWnd, IntPtr menu);
    [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool ModifyMenuW(IntPtr menu, uint position, uint flags, IntPtr idOrPopup, string? text);
    public const uint WM_INITMENUPOPUP = 0x0117;
    public const uint MF_BYPOSITION = 0x0400;
    public const uint MF_GRAYED = 0x0001;
    [DllImport("user32.dll")] public static extern bool DrawMenuBar(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint CheckMenuItem(IntPtr menu, uint id, uint flags);
    [DllImport("user32.dll")] public static extern bool CheckMenuRadioItem(IntPtr menu, uint first, uint last, uint check, uint flags);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateFontIndirectW(ref LOGFONT lf);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] public static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] public static extern uint SetBkColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] public static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern bool GetTextExtentPoint32W(IntPtr hdc, string text, int len, out SIZE size);

    [DllImport("comctl32.dll")] public static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX icc);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);
    [DllImport("uxtheme.dll", EntryPoint = "#135")] public static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] public static extern void FlushMenuThemes();
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT value, int size);

    [DllImport("ole32.dll")] public static extern int CoInitializeEx(IntPtr reserved, uint coInit);
    [DllImport("ole32.dll")] public static extern void CoTaskMemFree(IntPtr ptr);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SHBrowseForFolderW(ref BROWSEINFO bi);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandleW(string? name);

    public static int LoWord(IntPtr v) => (int)((long)v & 0xFFFF);
    public static int HiWord(IntPtr v) => (int)(((long)v >> 16) & 0xFFFF);
    public static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
    public static bool KeyDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

    public static string GetText(IntPtr hWnd)
    {
        int len = GetWindowTextLengthW(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowTextW(hWnd, sb, len + 1);
        return sb.ToString();
    }
}
