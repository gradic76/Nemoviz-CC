using System;
using System.IO;
using ClaudeChat.Native;

namespace ClaudeChat;

/// <summary>
/// The three sounds (author, 2026-11-08): Send when a message goes out, Bell when an answer is
/// complete, Typing looped while Claude works — but only while Live does not have the focus
/// (in Live the lines themselves are heard).
/// </summary>
internal sealed partial class MainWindow
{
    private readonly AudioEngine _audio = new();
    private Sound? _sndSend, _sndBell, _sndTyping;
    private object? _typingVoice;
    private const int TimerSound = 4;

    private static string SoundFolder => Path.Combine(AppContext.BaseDirectory, "sounds");

    private void InitSounds()
    {
        _sndSend = Sound.Load(Path.Combine(SoundFolder, "Send.wav"));
        _sndBell = Sound.Load(Path.Combine(SoundFolder, "Bell.wav"));
        _sndTyping = Sound.Load(Path.Combine(SoundFolder, "Typing.wav"));
        ApplySoundSettings();
        Win32.SetTimer(_hwnd, (IntPtr)TimerSound, 250, IntPtr.Zero);
    }

    private void ApplySoundSettings()
    {
        // No master volume any more (author, 2026-11-07): the mixer runs at full and each sound has its own.
        _audio.Volume = 1f;
        _audio.SetDevice(_settings.AudioDevice);
        _audio.KeepAwake = _settings.KeepAudioDeviceAwake;
        _audio.SetGain(_typingVoice, Gain(_settings.SoundTypingVolume));
        UpdateTypingSound();
    }

    private static float Gain(int percent) => Compat.Clamp(percent, 0, 100) / 100f;

    private void PlaySend() { if (_settings.SoundSend) _audio.Play(_sndSend, gain: Gain(_settings.SoundSendVolume)); }
    private void PlayBell() { if (_settings.SoundBell) _audio.Play(_sndBell, gain: Gain(_settings.SoundBellVolume)); }

    /// <summary>Called every 250 ms and on changes: Typing plays exactly while it should.</summary>
    private void UpdateTypingSound()
    {
        bool want = _settings.SoundTyping && _busy && Win32.GetFocus() != _live && WindowActive;
        if (want && _typingVoice == null) _typingVoice = _audio.Play(_sndTyping, loop: true, gain: Gain(_settings.SoundTypingVolume));
        else if (!want && _typingVoice != null) { _audio.Stop(_typingVoice); _typingVoice = null; }
    }

    /// <summary>Our window (or one of its dialogs) is the foreground window. Typing and the spoken
    /// answer stay quiet otherwise (author, 2026-11-08); the Bell still rings, as the notice.</summary>
    private bool WindowActive => Win32.GetAncestor(Win32.GetForegroundWindow(), 3 /* GA_ROOTOWNER */) == _hwnd;

    /// <summary>An answer finished while another program is in front: the taskbar button flashes
    /// until the window comes forward (author, 2026-11-08), the standard Windows notice.</summary>
    private void FlashTaskbar()
    {
        if (WindowActive) return;
        var info = new Win32.FLASHWINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.FLASHWINFO>(),
            hwnd = _hwnd,
            dwFlags = 0x3, // FLASHW_ALL with a count, as Electron's flashFrame does (Claude Desktop uses that)
            uCount = 4,
            dwTimeout = 0,
        };
        Win32.FlashWindowEx(ref info);
        _flashing = true;
        // Neither JAWS nor NVDA reports the flash (measured with both, 2026-11-08), so one short
        // sentence says it, heard from any program; the answer itself stays in History.
        _speech.Speak(_loc.Format("speak.answeredBackground", _settings.AppName), false);
    }

    private bool _flashing;

    /// <summary>Back in the window: the taskbar button stops asking for attention.</summary>
    private void StopFlashWhenActive()
    {
        if (!_flashing || !WindowActive) return;
        _flashing = false;
        var info = new Win32.FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.FLASHWINFO>(), hwnd = _hwnd, dwFlags = 0 /* FLASHW_STOP */ };
        Win32.FlashWindowEx(ref info);
    }

    /// <summary>Stop is only enabled while there is something to stop: an answer, a stop in
    /// progress, or a scheduled resume after the usage limit. Focus leaves it before it greys.</summary>
    private void UpdateStopButton()
    {
        bool active = _busy || _stopping || _resumeAt != null;
        if (Win32.IsWindowEnabled(_btnStop) == active) return;
        if (!active && Win32.GetFocus() == _btnStop) Win32.SetFocus(_live);
        Win32.EnableWindow(_btnStop, active);
    }

    /// <summary>For the Settings dialog's "Listen" buttons: plays at the volume chosen in the
    /// dialog, even before Apply.</summary>
    private void PreviewSound(string which, int percent)
    {
        var s = which switch { "send" => _sndSend, "bell" => _sndBell, _ => _sndTyping };
        _audio.Play(s, gain: Gain(percent));
    }
}
