using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// The Message field's context menu (Applications, Shift+F10, right click) and attachments.
/// Attachments are files sent with the next message as a list of paths; Claude opens them
/// itself (images, PDFs, text). Ways in: File dialog (Ctrl+U), Ctrl+V of files copied in
/// Explorer, Ctrl+V of an image (saved as PNG), and drag and drop onto the window.
/// To add an action, add one line to <see cref="BuildInputActions"/>.
/// </summary>
internal sealed partial class MainWindow
{
    private static readonly SubclassProcDelegate s_inputSubclass = InputSubclassStatic;
    private readonly List<string> _attachments = new();

    private static string PastedFolder => Path.Combine(Settings.AppDataFolder, "attachments");

    private void HookInputMenu()
    {
        Win32.SetWindowSubclass(_input, s_inputSubclass, (UIntPtr)2, IntPtr.Zero);
        Win32.DragAcceptFiles(_hwnd, true);
    }

    private static IntPtr InputSubclassStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr refData)
    {
        var self = s_instance;
        if (self != null)
        {
            try
            {
                if (msg == Win32.WM_CONTEXTMENU) { self.ShowInputMenu(lParam); return IntPtr.Zero; }
                // Files or an image on the clipboard become attachments instead of text.
                if (msg == Win32.WM_PASTE && self.TryPasteAttachments()) return IntPtr.Zero;
            }
            catch (Exception ex) { self.AddEntry(self._loc["role.system"], self._loc.Format("history.error", ex.Message)); }
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void ShowInputMenu(IntPtr lParam)
    {
        var actions = BuildInputActions();
        if (Environment.GetEnvironmentVariable("NEMOVIZ_DEBUG") == "1")
            AddEntry("debug", "input menu: " + string.Join(" | ", actions.Select(a => a == null ? "----" : a.Text + (a.Enabled ? "" : " (off)"))));
        IntPtr menu = Win32.CreatePopupMenu();
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            if (a == null) Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, IntPtr.Zero, null);
            else Win32.AppendMenuW(menu, Win32.MF_STRING | (a.Enabled ? 0 : Win32.MF_GRAYED), (IntPtr)(i + 1), a.Text);
        }

        // Keyboard: open at the caret; mouse: at the pointer.
        int x = (short)Win32.LoWord(lParam), y = (short)Win32.HiWord(lParam);
        Win32.GetWindowRect(_input, out RECT r);
        if (((long)lParam & 0xFFFFFFFF) == 0xFFFFFFFF || x < r.Left || x >= r.Right || y < r.Top || y >= r.Bottom)
        {
            Win32.SendMessageW(_input, Win32.EM_GETSEL, out int s, out _);
            long pos = (long)Win32.SendMessageW(_input, Win32.EM_POSFROMCHAR, (IntPtr)s, IntPtr.Zero);
            var pt = pos == -1 ? new POINT { X = 10, Y = 10 } : new POINT { X = (short)(pos & 0xFFFF), Y = (short)((pos >> 16) & 0xFFFF) };
            Win32.ClientToScreen(_input, ref pt);
            x = pt.X; y = pt.Y;
        }

        Win32.SetForegroundWindow(_hwnd);
        int chosen = Win32.TrackPopupMenuEx(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON, x, y, _hwnd, IntPtr.Zero);
        Win32.DestroyMenu(menu);
        Win32.SetFocus(_input);
        if (chosen > 0 && chosen <= actions.Count) actions[chosen - 1]?.Run();
    }

    private sealed record InputAction(string Text, Action Run, bool Enabled = true);

    /// <summary>The Message menu. A null entry is a separator.</summary>
    private List<InputAction?> BuildInputActions()
    {
        Win32.SendMessageW(_input, Win32.EM_GETSEL, out int s, out int e);
        bool selection = e > s;
        bool canUndo = (long)Win32.SendMessageW(_input, Win32.EM_CANUNDO, IntPtr.Zero, IntPtr.Zero) != 0;
        bool hasText = Win32.GetWindowTextLengthW(_input) > 0;

        var list = new List<InputAction?>
        {
            new(_loc["imenu.undo"], () => Win32.SendMessageW(_input, Win32.EM_UNDO, IntPtr.Zero, IntPtr.Zero), canUndo),
            null,
            new(_loc["imenu.cut"], () => Win32.SendMessageW(_input, Win32.WM_CUT, IntPtr.Zero, IntPtr.Zero), selection),
            new(_loc["imenu.copy"], () => Win32.SendMessageW(_input, Win32.WM_COPY, IntPtr.Zero, IntPtr.Zero), selection),
            new(_loc["imenu.paste"], () => Win32.SendMessageW(_input, Win32.WM_PASTE, IntPtr.Zero, IntPtr.Zero)),
            new(_loc["imenu.delete"], () => Win32.SendMessageW(_input, Win32.WM_CLEAR, IntPtr.Zero, IntPtr.Zero), selection),
            null,
            new(_loc["imenu.selectAll"], () => Win32.SendMessageW(_input, Win32.EM_SETSEL, IntPtr.Zero, (IntPtr)(-1)), hasText),
            null,
            new(_loc["imenu.attach"], AttachWithDialog),
        };
        foreach (var path in _attachments.ToList())
            list.Add(new(_loc.Format("imenu.remove", MenuLabel(Path.GetFileName(path))), () => RemoveAttachment(path)));
        if (_attachments.Count > 1)
            list.Add(new(_loc["imenu.removeAll"], () => { _attachments.Clear(); AttachmentsChanged(_loc["speak.attachCleared"]); }));
        return list;
    }

    // ------------------------------------------------------------------ attachments

    private void AddAttachments(IEnumerable<string> paths)
    {
        var added = new List<string>();
        foreach (var p in paths)
        {
            if (!File.Exists(p) && !Directory.Exists(p)) continue;
            if (_attachments.Contains(p, StringComparer.OrdinalIgnoreCase)) continue;
            _attachments.Add(p);
            added.Add(Path.GetFileName(p.TrimEnd('\\')));
        }
        if (added.Count == 0) { Win32.MessageBeep(0); return; }
        AttachmentsChanged(_loc.Format("speak.attached", string.Join(", ", added), _attachments.Count));
    }

    private void RemoveAttachment(string path)
    {
        _attachments.Remove(path);
        AttachmentsChanged(_loc.Format("speak.attachRemoved", Path.GetFileName(path), _attachments.Count));
    }

    /// <summary>The Message label carries the count, so it is heard on entering the field.</summary>
    private void AttachmentsChanged(string spoken)
    {
        Win32.SetWindowTextW(_lblInput, InputLabelText());
        _speech.Speak(spoken, true);
    }

    private string InputLabelText() => _attachments.Count == 0
        ? _loc["label.input"]
        : _loc.Format("label.inputAttachments", _loc["label.input"], _attachments.Count);

    /// <summary>The text that goes to Claude: the message, then one line per attachment.</summary>
    private string WithAttachments(string text)
    {
        if (_attachments.Count == 0) return text;
        var sb = new StringBuilder(text);
        if (sb.Length > 0) sb.Append("\n\n");
        sb.Append(_loc["attach.intro"]);
        foreach (var p in _attachments) sb.Append('\n').Append(_loc.Format("attach.line", p));
        _attachments.Clear();
        Win32.SetWindowTextW(_lblInput, _loc["label.input"]);
        return sb.ToString();
    }

    private void AttachWithDialog()
    {
        var files = PickFiles(_hwnd, _project);
        if (files != null) AddAttachments(files);
    }

    /// <summary>The Open dialog with multiple selection; null when cancelled.</summary>
    private List<string>? PickFiles(IntPtr owner, string initialDir, string? title = null)
    {
        const int max = 32 * 1024;
        IntPtr buffer = Marshal.AllocHGlobal(max * 2);
        try
        {
            Marshal.WriteInt16(buffer, 0, 0);
            var ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = owner,
                lpstrFilter = _loc["attach.filter"].Replace('|', '\0') + "\0\0",
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = max,
                lpstrInitialDir = initialDir,
                lpstrTitle = title ?? _loc["attach.title"],
                Flags = Win32.OFN_EXPLORER | Win32.OFN_ALLOWMULTISELECT | Win32.OFN_FILEMUSTEXIST | Win32.OFN_PATHMUSTEXIST,
            };
            if (!Win32.GetOpenFileNameW(ref ofn)) return null;

            // One file: "C:\dir\file". Several: "C:\dir" \0 "a" \0 "b" \0 \0.
            var parts = new List<string>();
            int offset = 0;
            while (true)
            {
                string part = Marshal.PtrToStringUni(buffer + offset * 2) ?? "";
                if (part.Length == 0) break;
                parts.Add(part);
                offset += part.Length + 1;
            }
            if (parts.Count == 1) return parts;
            return parts.Skip(1).Select(n => Path.Combine(parts[0], n)).ToList();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private void OnDropFiles(IntPtr drop)
    {
        try
        {
            uint count = Win32.DragQueryFileW(drop, 0xFFFFFFFF, null, 0);
            var paths = new List<string>();
            for (uint i = 0; i < count; i++)
            {
                var sb = new StringBuilder(32768);
                Win32.DragQueryFileW(drop, i, sb, (uint)sb.Capacity);
                paths.Add(sb.ToString());
            }
            AddAttachments(paths);
            Win32.SetFocus(_input);
        }
        finally { Win32.DragFinish(drop); }
    }

    /// <summary>
    /// Ctrl+V in Message: files copied in Explorer, or an image, become attachments. Plain text
    /// pastes as usual (returns false). An image is saved as PNG in %APPDATA%\ClaudeChat\attachments.
    /// </summary>
    private bool TryPasteAttachments()
    {
        bool files = Win32.IsClipboardFormatAvailable(Win32.CF_HDROP);
        uint pngFormat = Win32.RegisterClipboardFormatW("PNG");
        bool png = Win32.IsClipboardFormatAvailable(pngFormat);
        bool dib = Win32.IsClipboardFormatAvailable(Win32.CF_DIB);
        bool text = Win32.IsClipboardFormatAvailable(Win32.CF_UNICODETEXT);
        if (!files && (text || (!png && !dib))) return false;
        if (!Win32.OpenClipboard(_hwnd)) return false;

        var paths = new List<string>();
        try
        {
            if (files)
            {
                IntPtr drop = Win32.GetClipboardData(Win32.CF_HDROP);
                uint count = Win32.DragQueryFileW(drop, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    var sb = new StringBuilder(32768);
                    Win32.DragQueryFileW(drop, i, sb, (uint)sb.Capacity);
                    paths.Add(sb.ToString());
                }
            }
            else
            {
                byte[]? image = png ? ClipboardBytes(pngFormat) : DibToPng(ClipboardBytes(Win32.CF_DIB));
                if (image == null) return false;
                Directory.CreateDirectory(PastedFolder);
                string file = Path.Combine(PastedFolder, $"slika-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                File.WriteAllBytes(file, image);
                paths.Add(file);
            }
        }
        finally { Win32.CloseClipboard(); }

        AddAttachments(paths);
        return true;
    }

    private static byte[]? ClipboardBytes(uint format)
    {
        IntPtr h = Win32.GetClipboardData(format);
        if (h == IntPtr.Zero) return null;
        IntPtr p = Win32.GlobalLock(h);
        if (p == IntPtr.Zero) return null;
        try
        {
            var bytes = new byte[(int)(ulong)Win32.GlobalSize(h)];
            Marshal.Copy(p, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { Win32.GlobalUnlock(h); }
    }

    /// <summary>A 24- or 32-bit clipboard DIB as a PNG (RGBA, no filter, zlib). Null if unsupported.</summary>
    private static byte[]? DibToPng(byte[]? dib)
    {
        if (dib == null || dib.Length < 40) return null;
        int headerSize = BitConverter.ToInt32(dib, 0);
        int width = BitConverter.ToInt32(dib, 4);
        int height = BitConverter.ToInt32(dib, 8);
        int bpp = BitConverter.ToUInt16(dib, 14);
        int compression = BitConverter.ToInt32(dib, 16);
        int colorsUsed = BitConverter.ToInt32(dib, 32);
        if (width <= 0 || height == 0 || (bpp != 24 && bpp != 32) || (compression != 0 && compression != 3)) return null;

        bool bottomUp = height > 0;
        height = Math.Abs(height);
        int offset = headerSize + (compression == 3 && headerSize == 40 ? 12 : 0) + colorsUsed * 4;
        int stride = ((width * bpp + 31) / 32) * 4;
        if (offset + stride * height > dib.Length) return null;

        // Screenshots often carry 32-bit pixels with an empty alpha byte: treat as opaque.
        bool alphaUsed = false;
        if (bpp == 32)
            for (int y = 0; y < height && !alphaUsed; y++)
                for (int x = 0; x < width; x++)
                    if (dib[offset + y * stride + x * 4 + 3] != 0) { alphaUsed = true; break; }

        var raw = new byte[(width * 4 + 1) * height];
        int bytesPerPixel = bpp / 8;
        for (int y = 0; y < height; y++)
        {
            int src = offset + (bottomUp ? height - 1 - y : y) * stride;
            int dst = y * (width * 4 + 1);
            raw[dst++] = 0; // filter: none
            for (int x = 0; x < width; x++, src += bytesPerPixel)
            {
                raw[dst++] = dib[src + 2];
                raw[dst++] = dib[src + 1];
                raw[dst++] = dib[src];
                raw[dst++] = bpp == 32 && alphaUsed ? dib[src + 3] : (byte)255;
            }
        }

        using var compressed = new MemoryStream();
        Compat.WriteZlib(compressed, raw);

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // colour type RGBA
        PngChunk(png, "IHDR", ihdr);
        PngChunk(png, "IDAT", compressed.ToArray());
        PngChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteBigEndian(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static void PngChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBigEndian(len, 0, data.Length);
        s.Write(len, 0, len.Length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes, 0, typeBytes.Length);
        s.Write(data, 0, data.Length);
        uint crc = Crc32(typeBytes, 0xFFFFFFFF);
        crc = Crc32(data, crc) ^ 0xFFFFFFFF;
        var c = new byte[4];
        WriteBigEndian(c, 0, (int)crc);
        s.Write(c, 0, c.Length);
    }

    private static uint[]? s_crcTable;

    private static uint Crc32(byte[] data, uint crc)
    {
        if (s_crcTable == null)
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            s_crcTable = t;
        }
        foreach (byte b in data) crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
