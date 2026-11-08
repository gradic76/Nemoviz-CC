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

    private static float Gain(int percent) => Math.Clamp(percent, 0, 100) / 100f;

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
