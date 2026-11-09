using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudeChat;

/// <summary>
/// Session → Available on the phone (author, 2026-11-09): the open conversation can also be typed
/// into from the Claude mobile app, as a second keyboard. It uses Claude Code's remote_control
/// control request on the process this window already runs, so there is no console and no second
/// session, and the same permissions apply. The request is not documented (found in claude.exe
/// 2.1.317, then measured), so a future Claude Code may change it; a refusal is reported in History.
/// A new process (changed model, switched conversation) reattaches to the phone's session.
/// </summary>
internal sealed partial class MainWindow
{
    private const int IdmMobile = 249;
    private string? _mobileBridgeId, _mobileUrl;
    private bool _mobileAnnounce, _mobileReattaching, _mobileNeedsEnable;
    private DateTime _mobileRetryAt = DateTime.MinValue;
    private int _mobileFailures;

    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);

    /// <summary>The conversation can only be reached while the computer is awake; the screen may still turn off.</summary>
    private void ApplyKeepAwake()
    {
        const uint Continuous = 0x80000000, SystemRequired = 0x00000001;
        SetThreadExecutionState(_settings.MobileAccess ? Continuous | SystemRequired : Continuous);
    }

    private void OnMobileToggled()
    {
        bool on = !_settings.MobileAccess;
        _settings.MobileAccess = on;
        _settings.SaveUserValue("mobileAccess", on);
        BuildMenu();
        ApplyKeepAwake();
        if (on)
        {
            _mobileAnnounce = true;
            _mobileFailures = 0;
            _mobileRetryAt = DateTime.MinValue;
            _mobileUrl = null;
            if (_session is { IsRunning: true }) EnableMobile();
            else EnsureSession(); // which turns it on as soon as the process is up
            return;
        }
        _session?.DisableRemoteControl();
        _mobileUrl = null;
        _mobileNeedsEnable = false;
        AddEntry(_loc["role.system"], _loc["mobile.off"]);
        _speech.Speak(_loc["mobile.off"], true);
    }

    private void ForgetMobileSession()
    {
        _mobileBridgeId = null;
        _mobileUrl = null;
        _mobileNeedsEnable = false;
    }

    private void EnableMobile()
    {
        string? reattach = _mobileBridgeId;
        if (reattach == null && _lastSessionId != null) _state.MobileBridges.TryGetValue(_lastSessionId, out reattach);
        _mobileReattaching = reattach != null;
        _session?.EnableRemoteControl(Path.GetFileName(_project.TrimEnd('\\')), reattach);
    }

    private void RememberMobileBridge()
    {
        if (_mobileBridgeId == null || _lastSessionId == null) return;
        if (_state.MobileBridges.TryGetValue(_lastSessionId, out var known) && known == _mobileBridgeId) return;
        _state.MobileBridges[_lastSessionId] = _mobileBridgeId;
        _state.Save();
    }

    private void OnRemoteEnabled(string url, string bridgeId)
    {
        _mobileFailures = 0;
        _mobileBridgeId = bridgeId;
        RememberMobileBridge();
        if (!_busy) SetLive(_loc["live.idle"]);
        if (url == _mobileUrl) return; // the same phone session after a new process
        _mobileUrl = url;
        AddEntry(_loc["role.system"], _loc.Format("mobile.on", url));
        if (_mobileAnnounce) _speech.Speak(_loc["mobile.onSpoken"], true);
        _mobileAnnounce = false;
    }

    private void OnRemoteFailed(string message)
    {
        if (_mobileReattaching)
        {
            // The phone's old session has expired (about four hours): a fresh one will do.
            if (_lastSessionId != null) _state.MobileBridges.Remove(_lastSessionId);
            _mobileBridgeId = null;
            _mobileReattaching = false;
            _session?.EnableRemoteControl(Path.GetFileName(_project.TrimEnd('\\')), null);
            return;
        }
        AddEntry(_loc["role.system"], _loc.Format("mobile.failed", message));
        ScheduleMobileRetry();
    }

    private void OnBridgeState(string state)
    {
        if (state != "failed" || !_settings.MobileAccess) return;
        AddLive(_loc["mobile.lost"]);
        _mobileNeedsEnable = true;
        ScheduleMobileRetry();
    }

    private void ScheduleMobileRetry()
    {
        _mobileFailures++;
        int seconds = Math.Min(3600, 30 << Math.Min(_mobileFailures, 7));
        _mobileRetryAt = DateTime.UtcNow.AddSeconds(seconds);
    }

    /// <summary>A message typed on the phone: Claude Code is already answering it.</summary>
    private void OnRemoteMessage(string text, int images)
    {
        for (int i = 0; i < images; i++) text = (text + " " + _loc["history.image"]).Trim();
        AddEntry(_loc.Format("role.userMobile", _settings.UserName ?? _loc["role.user"]), text);
        if (_busy) return;
        CancelResumeTimer();
        _busy = true;
        _turnText.Clear();
        _liveLine.Clear();
        SetLive(_loc["live.working"]);
    }

    /// <summary>Every tick: with the box checked, a conversation must always have a process to reach it.</summary>
    private void KeepMobileAlive()
    {
        if (!_settings.MobileAccess || _busy || _stopping || DateTime.UtcNow < _mobileRetryAt) return;
        if (_session is not { IsRunning: true })
        {
            ScheduleMobileRetry();
            _claudeExe ??= ClaudeLocator.Find(_settings.ClaudePath);
            if (_claudeExe != null) EnsureSession();
            return;
        }
        if (_mobileNeedsEnable)
        {
            _mobileNeedsEnable = false;
            EnableMobile();
        }
    }
}
