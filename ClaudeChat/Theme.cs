using System;
using ClaudeChat.Native;
using Microsoft.Win32;

namespace ClaudeChat;

public enum ThemeKind { Light, Dark, HighContrast }

/// <summary>
/// Colour tokens. Every colour the window paints comes from here, so a new theme is a new palette,
/// not new drawing code. High contrast always uses the system colours the user chose.
/// </summary>
public sealed class Theme : IDisposable
{
    public ThemeKind Kind { get; }
    public uint WindowBack { get; }
    public uint WindowText { get; }
    public uint FieldBack { get; }
    public uint FieldText { get; }
    public uint ReadOnlyFieldBack { get; }
    public uint LabelText { get; }

    public IntPtr WindowBrush { get; }
    public IntPtr FieldBrush { get; }
    public IntPtr ReadOnlyFieldBrush { get; }

    private Theme(ThemeKind kind, uint winBack, uint winText, uint fieldBack, uint fieldText, uint roBack, uint label)
    {
        Kind = kind;
        WindowBack = winBack; WindowText = winText;
        FieldBack = fieldBack; FieldText = fieldText;
        ReadOnlyFieldBack = roBack; LabelText = label;
        WindowBrush = Win32.CreateSolidBrush(winBack);
        FieldBrush = Win32.CreateSolidBrush(fieldBack);
        ReadOnlyFieldBrush = Win32.CreateSolidBrush(roBack);
    }

    public bool IsDark => Kind == ThemeKind.Dark;

    public static Theme Create(string setting)
    {
        if (IsHighContrast())
        {
            uint win = Win32.GetSysColor(Win32.COLOR_WINDOW);
            uint text = Win32.GetSysColor(Win32.COLOR_WINDOWTEXT);
            uint face = Win32.GetSysColor(Win32.COLOR_BTNFACE);
            uint btnText = Win32.GetSysColor(Win32.COLOR_BTNTEXT);
            return new Theme(ThemeKind.HighContrast, face, btnText, win, text, win, btnText);
        }

        bool dark = setting.ToLowerInvariant() switch
        {
            "dark" => true,
            "light" => false,
            _ => SystemPrefersDark(),
        };

        // Text/background pairs keep a contrast ratio well above WCAG AA (4.5:1).
        return dark
            ? new Theme(ThemeKind.Dark,
                winBack: Win32.Rgb(0x20, 0x20, 0x20), winText: Win32.Rgb(0xF0, 0xF0, 0xF0),
                fieldBack: Win32.Rgb(0x2B, 0x2B, 0x2B), fieldText: Win32.Rgb(0xF5, 0xF5, 0xF5),
                roBack: Win32.Rgb(0x26, 0x26, 0x26), label: Win32.Rgb(0xE6, 0xE6, 0xE6))
            : new Theme(ThemeKind.Light,
                winBack: Win32.Rgb(0xF3, 0xF3, 0xF3), winText: Win32.Rgb(0x1A, 0x1A, 0x1A),
                fieldBack: Win32.Rgb(0xFF, 0xFF, 0xFF), fieldText: Win32.Rgb(0x10, 0x10, 0x10),
                roBack: Win32.Rgb(0xFB, 0xFB, 0xFB), label: Win32.Rgb(0x1A, 0x1A, 0x1A));
    }

    public static bool IsHighContrast()
    {
        var hc = new HIGHCONTRAST { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<HIGHCONTRAST>() };
        return Win32.SystemParametersInfoW(Win32.SPI_GETHIGHCONTRAST, hc.cbSize, ref hc, 0)
               && (hc.dwFlags & Win32.HCF_HIGHCONTRASTON) != 0;
    }

    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    /// <summary>Title bar, popup menus and scroll bars follow the theme (Windows 10 1809+).</summary>
    public void ApplyToTopLevel(IntPtr hwnd)
    {
        int dark = IsDark ? 1 : 0;
        Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        try
        {
            // Undocumented but stable since 1903: 2 = force dark, 3 = force light.
            Win32.SetPreferredAppMode(IsDark ? 2 : 3);
            Win32.FlushMenuThemes();
        }
        catch (EntryPointNotFoundException) { }
    }

    public enum ControlRole { Field, PushButton, CheckBox, Label, Combo }

    public void ApplyToControl(IntPtr hwnd, ControlRole role)
    {
        if (Kind == ThemeKind.HighContrast) { Win32.SetWindowTheme(hwnd, null, null); return; }
        switch (role)
        {
            case ControlRole.Field:
            case ControlRole.PushButton:
                Win32.SetWindowTheme(hwnd, IsDark ? "DarkMode_Explorer" : "Explorer", null);
                break;
            case ControlRole.Combo:
                // "DarkMode_CFD" is the dark combo style the common file dialog uses.
                Win32.SetWindowTheme(hwnd, IsDark ? "DarkMode_CFD" : "Explorer", null);
                break;
            case ControlRole.CheckBox:
                // Themed check boxes ignore the text colour, so in dark mode the visual style is
                // switched off for them and the colour from WM_CTLCOLORSTATIC applies.
                if (IsDark) Win32.SetWindowTheme(hwnd, "", "");
                else Win32.SetWindowTheme(hwnd, "Explorer", null);
                break;
        }
    }

    public void Dispose()
    {
        Win32.DeleteObject(WindowBrush);
        Win32.DeleteObject(FieldBrush);
        Win32.DeleteObject(ReadOnlyFieldBrush);
    }
}
