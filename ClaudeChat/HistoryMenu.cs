using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// The context menu of History (Applications key, Shift+F10, right click). It works on the one
/// message under the caret (or under the mouse) and offers what that message has: always copy,
/// read and save; each code block; each file or folder it names that exists on disk.
/// To add an action, add one line to <see cref="BuildHistoryActions"/>.
/// </summary>
internal sealed partial class MainWindow
{
    private sealed record MenuAction(string Text, Action Run);

    private static readonly SubclassProcDelegate s_historySubclass = HistorySubclassStatic;

    private static readonly Regex CodeBlock = new(@"```[^\r\n]*\r?\n(.*?)\r?\n?```", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex InlineCode = new(@"`([^`\r\n]{3,})`", RegexOptions.Compiled);
    // Overlapping on purpose: every "X:\" starts its own candidate, so a second path on the same
    // line is not swallowed by the first ("D:\Player je sad D:\Claude\…").
    private static readonly Regex WindowsPath = new(@"(?=(?<![A-Za-z])([A-Za-z]:\\[^\r\n""'<>|*?`]+))", RegexOptions.Compiled);

    private void HookHistoryMenu()
    {
        if (!Win32.SetWindowSubclass(_history, s_historySubclass, (UIntPtr)1, IntPtr.Zero))
            _hookFailed = true; // reported once History exists (see Run)
    }

    private bool _hookFailed;

    private static IntPtr HistorySubclassStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr refData)
    {
        if (msg == Win32.WM_CONTEXTMENU && s_instance != null)
        {
            try { s_instance.ShowHistoryMenu(lParam); }
            catch (Exception ex) { s_instance.AddEntry(s_instance._loc["role.system"], s_instance._loc.Format("history.error", ex.Message)); }
            return IntPtr.Zero; // the EDIT's own Undo/Cut/Copy menu is replaced
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>lParam is the mouse position in screen coordinates, or -1 from the keyboard.</summary>
    private void ShowHistoryMenu(IntPtr lParam)
    {
        int index = EntryIndexForMenu(lParam, out POINT at);
        if (Environment.GetEnvironmentVariable("NEMOVIZ_DEBUG") == "1")
            AddEntry("debug", $"context menu: lParam={(long)lParam}, entry {index} of {_entries.Count}, starts {_entryStarts.Count}"
                + (index >= 0 ? $", start {_entryStarts[index]}, text: {MenuLabel(_entries[index].Text)}" : ""));
        if (index < 0) { Win32.MessageBeep(0); return; }

        var actions = BuildHistoryActions(index);
        if (Environment.GetEnvironmentVariable("NEMOVIZ_DEBUG") == "1")
            AddEntry("debug", $"menu for entry {index}: " + string.Join(" | ", actions.Select(a => a?.Text ?? "----")));
        IntPtr menu = Win32.CreatePopupMenu();
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] == null) Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, IntPtr.Zero, null);
            else Win32.AppendMenuW(menu, Win32.MF_STRING, (IntPtr)(i + 1), actions[i]!.Text);
        }
        Win32.SetForegroundWindow(_hwnd); // a popup menu needs its owner in front, or it does not stay open
        int chosen = Win32.TrackPopupMenuEx(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON, at.X, at.Y, _hwnd, IntPtr.Zero);
        Win32.DestroyMenu(menu);
        Win32.SetFocus(_history);
        if (chosen > 0 && chosen <= actions.Count) actions[chosen - 1]?.Run();
    }

    /// <summary>The entry to act on, and where to open the menu.</summary>
    private int EntryIndexForMenu(IntPtr lParam, out POINT at)
    {
        int x = (short)Win32.LoWord(lParam), y = (short)Win32.HiWord(lParam);
        int charIndex;
        // From the keyboard lParam is -1 in its low 32 bits; on 64-bit it may arrive without the sign.
        // Anything that is not a point inside History is treated as the keyboard.
        Win32.GetWindowRect(_history, out RECT hr);
        bool keyboard = ((long)lParam & 0xFFFFFFFF) == 0xFFFFFFFF
                        || x < hr.Left || x >= hr.Right || y < hr.Top || y >= hr.Bottom;
        if (keyboard)
        {
            // Keyboard: the caret's message; the menu opens at the caret.
            Win32.SendMessageW(_history, Win32.EM_GETSEL, out int selStart, out _);
            charIndex = selStart;
            long pos = (long)Win32.SendMessageW(_history, Win32.EM_POSFROMCHAR, (IntPtr)charIndex, IntPtr.Zero);
            at = new POINT { X = (short)(pos & 0xFFFF), Y = (short)((pos >> 16) & 0xFFFF) };
            if (pos == -1) { Win32.GetClientRect(_history, out RECT rc); at = new POINT { X = rc.Width / 3, Y = rc.Height / 3 }; }
            Win32.ClientToScreen(_history, ref at);
        }
        else
        {
            // Mouse: the message under the pointer. EM_CHARFROMPOS gives a 16-bit character index,
            // so the line (high word) is turned into a full index instead.
            at = new POINT { X = x, Y = y };
            var client = at;
            Win32.ScreenToClient(_history, ref client);
            long r = (long)Win32.SendMessageW(_history, Win32.EM_CHARFROMPOS, IntPtr.Zero, (IntPtr)((client.Y << 16) | (client.X & 0xFFFF)));
            int line = (int)((r >> 16) & 0xFFFF);
            charIndex = (int)(long)Win32.SendMessageW(_history, Win32.EM_LINEINDEX, (IntPtr)line, IntPtr.Zero);
        }

        int i = _entryStarts.BinarySearch(charIndex);
        if (i < 0) i = ~i - 1;
        return i >= 0 && i < _entries.Count ? i : -1;
    }

    /// <summary>The menu for one message. A null entry is a separator.</summary>
    private List<MenuAction?> BuildHistoryActions(int index)
    {
        var e = _entries[index];
        // No "open in a reader window": History is read, selected and copied in place
        // (author, 2026-11-08 — one place for it, not two).
        var list = new List<MenuAction?>
        {
            new(_loc["hmenu.copy"], () => Copy(e.Text)),
            new(_loc["hmenu.save"], () => SaveAs(e)),
        };

        var code = CodeBlock.Matches(e.Text).Select(m => m.Groups[1].Value).ToList();
        if (code.Count == 0) code = InlineCode.Matches(e.Text).Select(m => m.Groups[1].Value).Distinct().Take(10).ToList();
        if (code.Count > 0)
        {
            list.Add(null);
            foreach (var c in code)
                list.Add(new(_loc.Format("hmenu.copyCode", MenuLabel(c)), () => Copy(c)));
        }

        var paths = PathsIn(e.Text);
        if (paths.Count > 0)
        {
            list.Add(null);
            foreach (var p in paths)
            {
                string name = MenuLabel(Path.GetFileName(p.TrimEnd('\\')) is { Length: > 0 } f ? f : p);
                if (Directory.Exists(p))
                {
                    list.Add(new(_loc.Format("hmenu.openFolder", name), () => ShowInExplorer(p)));
                    continue;
                }
                // Programs and scripts are never started from here, only shown.
                if (!RunsSomething(p)) list.Add(new(_loc.Format("hmenu.open", name), () => OpenPath(p)));
                list.Add(new(_loc.Format("hmenu.show", MenuLabel(p)), () => ShowInExplorer(p)));
            }
        }
        return list;
    }

    /// <summary>Paths written in the message that exist now, longest match first, at most eight.</summary>
    private static List<string> PathsIn(string text)
    {
        var found = new List<string>();
        foreach (Match m in WindowsPath.Matches(text))
        {
            // A path may run into the following words; shorten it to the longest part that exists.
            string candidate = m.Groups[1].Value.TrimEnd('.', ',', ';', ':', ')', ' ');
            while (candidate.Length > 3 && !File.Exists(candidate) && !Directory.Exists(candidate))
            {
                int cut = Math.Max(candidate.LastIndexOf(' '), candidate.LastIndexOf('\\'));
                if (cut <= 2) break;
                candidate = candidate[..cut].TrimEnd('.', ',', ';', ':', ')', ' ', '\\');
            }
            if ((File.Exists(candidate) || Directory.Exists(candidate)) && candidate.Length > 3
                && !found.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                found.Add(candidate);
            if (found.Count >= 8) break;
        }
        return found;
    }

    private static readonly HashSet<string> Runnable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".cmd", ".bat", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".msi", ".msix", ".appx", ".lnk", ".scr", ".pif", ".reg", ".hta", ".cpl", ".jar", ".py", ".sh",
    };

    private static bool RunsSomething(string file) => Runnable.Contains(Path.GetExtension(file));

    private static string MenuLabel(string s)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim();
        if (s.Length > 50) s = s[..50] + "…";
        return s.Replace("&", "&&");
    }

    private void Copy(string text)
    {
        if (Win32.SetClipboardText(_hwnd, Crlf(text))) _speech.Speak(_loc["speak.copied"], true);
        else Win32.MessageBeep(0);
    }

    private void SaveAs(HistoryEntry e)
    {
        const int max = 1024;
        IntPtr buffer = Marshal.AllocHGlobal(max * 2);
        try
        {
            string suggested = _loc["save.defaultName"];
            var chars = (suggested + "\0").ToCharArray();
            Marshal.Copy(chars, 0, buffer, chars.Length);
            var ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = _hwnd,
                lpstrFilter = _loc["save.filter"].Replace('|', '\0') + "\0\0",
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = max,
                lpstrInitialDir = _project,
                lpstrTitle = _loc["save.title"],
                lpstrDefExt = "txt",
                Flags = Win32.OFN_EXPLORER | Win32.OFN_OVERWRITEPROMPT | Win32.OFN_PATHMUSTEXIST,
            };
            if (!Win32.GetSaveFileNameW(ref ofn)) return;
            string file = Marshal.PtrToStringUni(buffer) ?? "";
            if (file.Length == 0) return;
            File.WriteAllText(file, Header(e) + "\r\n" + Crlf(e.Text) + "\r\n", new UTF8Encoding(false));
            _speech.Speak(_loc["speak.saved"], true);
        }
        catch (Exception ex) { AddEntry(_loc["role.system"], _loc.Format("history.error", ex.Message)); }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { AddEntry(_loc["role.system"], _loc.Format("history.error", ex.Message)); }
    }

    /// <summary>A folder opens in Explorer; a file opens its folder with the file selected.</summary>
    private void ShowInExplorer(string path)
    {
        try
        {
            string args = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex) { AddEntry(_loc["role.system"], _loc.Format("history.error", ex.Message)); }
    }
}
