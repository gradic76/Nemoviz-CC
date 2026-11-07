using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudeChat;

/// <summary>Speaks text through the running screen reader. Silently does nothing when none is reachable.</summary>
public interface ISpeech
{
    string Name { get; }
    bool Speak(string text, bool interrupt);
    void Silence();
}

public static class SpeechFactory
{
    /// <summary>"auto" tries JAWS first (the primary reader), then NVDA.</summary>
    public static ISpeech Create(string setting)
    {
        switch (setting.ToLowerInvariant())
        {
            case "none": return new NoSpeech();
            case "jaws": return new JawsSpeech();
            case "nvda": return new NvdaSpeech();
            default: return new AutoSpeech(new ISpeech[] { new JawsSpeech(), new NvdaSpeech() });
        }
    }
}

internal sealed class NoSpeech : ISpeech
{
    public string Name => "none";
    public bool Speak(string text, bool interrupt) => false;
    public void Silence() { }
}

/// <summary>Uses whichever reader answers; asks again each time, since readers start and stop.</summary>
internal sealed class AutoSpeech(ISpeech[] chain) : ISpeech
{
    public string Name => "auto";
    public bool Speak(string text, bool interrupt)
    {
        foreach (var s in chain) if (s.Speak(text, interrupt)) return true;
        return false;
    }
    public void Silence() { foreach (var s in chain) s.Silence(); }
}

/// <summary>JAWS through its COM object FreedomSci.JawsApi (SayString / StopSpeech).</summary>
internal sealed class JawsSpeech : ISpeech
{
    private object? _api;
    public string Name => "JAWS";

    private static bool JawsRunning() => System.Diagnostics.Process.GetProcessesByName("jfw").Length > 0;

    private dynamic? Api()
    {
        if (!JawsRunning()) return null;
        if (_api != null) return _api;
        var type = Type.GetTypeFromProgID("FreedomSci.JawsApi");
        if (type == null) return null;
        _api = Activator.CreateInstance(type);
        return _api;
    }

    public bool Speak(string text, bool interrupt)
    {
        try
        {
            dynamic? api = Api();
            if (api == null) return false;
            // The return value is not reliable across JAWS versions (JAWS speaks and still
            // reports false or nothing); a call that does not throw counts as spoken.
            api.SayString(text, interrupt);
            return true;
        }
        catch { _api = null; return false; }
    }

    public void Silence()
    {
        try { dynamic? api = Api(); api?.StopSpeech(); } catch { _api = null; }
    }
}

/// <summary>
/// NVDA through nvdaControllerClient.dll (shipped with NVDA's developer kit, not with NVDA itself).
/// Put nvdaControllerClient.dll (64-bit) next to ClaudeChat.exe to enable it.
/// </summary>
internal sealed class NvdaSpeech : ISpeech
{
    public string Name => "NVDA";
    private static readonly bool Present = File.Exists(Path.Combine(AppContext.BaseDirectory, "nvdaControllerClient.dll"));

    [DllImport("nvdaControllerClient.dll", CharSet = CharSet.Unicode)] private static extern int nvdaController_testIfRunning();
    [DllImport("nvdaControllerClient.dll", CharSet = CharSet.Unicode)] private static extern int nvdaController_speakText(string text);
    [DllImport("nvdaControllerClient.dll")] private static extern int nvdaController_cancelSpeech();

    public bool Speak(string text, bool interrupt)
    {
        if (!Present) return false;
        try
        {
            if (nvdaController_testIfRunning() != 0) return false;
            if (interrupt) nvdaController_cancelSpeech();
            return nvdaController_speakText(text) == 0;
        }
        catch { return false; }
    }

    public void Silence()
    {
        if (!Present) return;
        try { nvdaController_cancelSpeech(); } catch { }
    }
}
