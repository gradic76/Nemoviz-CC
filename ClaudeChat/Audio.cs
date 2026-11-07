using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ClaudeChat;

/// <summary>A sound decoded to 44.1 kHz 16-bit stereo, ready to mix.</summary>
public sealed class Sound
{
    public short[] Samples { get; }  // interleaved L R
    private Sound(short[] s) { Samples = s; }

    /// <summary>Reads a PCM WAV (8 or 16 bit, mono or stereo, any rate). Null if it is not one.</summary>
    public static Sound? Load(string file)
    {
        try
        {
            byte[] b = File.ReadAllBytes(file);
            if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE") return null;
            int pos = 12, channels = 0, rate = 0, bits = 0, format = 0, dataAt = -1, dataLen = 0;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int len = BitConverter.ToInt32(b, pos + 4);
                if (id == "fmt ")
                {
                    format = BitConverter.ToInt16(b, pos + 8);
                    channels = BitConverter.ToInt16(b, pos + 10);
                    rate = BitConverter.ToInt32(b, pos + 12);
                    bits = BitConverter.ToInt16(b, pos + 22);
                }
                else if (id == "data") { dataAt = pos + 8; dataLen = Math.Min(len, b.Length - dataAt); break; }
                pos += 8 + len + (len & 1);
            }
            if (dataAt < 0 || format != 1 || (bits != 8 && bits != 16) || channels < 1 || channels > 2 || rate <= 0) return null;

            int bytesPerSample = bits / 8;
            int frames = dataLen / (bytesPerSample * channels);
            float Sample(int frame, int ch)
            {
                int at = dataAt + (frame * channels + Math.Min(ch, channels - 1)) * bytesPerSample;
                return bits == 16 ? BitConverter.ToInt16(b, at) / 32768f : (b[at] - 128) / 128f;
            }

            // Linear resampling to the mixer's 44.1 kHz.
            int outFrames = (int)((long)frames * AudioEngine.Rate / rate);
            var outS = new short[outFrames * 2];
            for (int i = 0; i < outFrames; i++)
            {
                double src = (double)i * rate / AudioEngine.Rate;
                int i0 = (int)src, i1 = Math.Min(i0 + 1, frames - 1);
                float f = (float)(src - i0);
                for (int ch = 0; ch < 2; ch++)
                {
                    float v = Sample(i0, ch) * (1 - f) + Sample(i1, ch) * f;
                    outS[i * 2 + ch] = (short)Math.Clamp(v * 32767f, -32768f, 32767f);
                }
            }
            return new Sound(outS);
        }
        catch { return null; }
    }
}

/// <summary>
/// A small mixer on waveOut: several sounds at once (a looping one among them), a master volume
/// and a chosen output device. PlaySound cannot do either — it plays one sound, on the default
/// device. The device is opened only while something plays.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    public const int Rate = 44100;
    private const int Frames = 2048;     // ~46 ms per buffer
    private const int Buffers = 4;
    private const uint WaveMapper = 0xFFFFFFFF;

    private sealed class Voice { public Sound Sound = null!; public int Pos; public bool Loop; public volatile float Gain = 1f; }

    private readonly object _lock = new();
    private readonly List<Voice> _voices = new();
    private Thread? _thread;
    private volatile bool _stop;
    private float _volume = 0.8f;
    private uint _device = WaveMapper;

    public float Volume { get => _volume; set => _volume = Math.Clamp(value, 0f, 1f); }

    private volatile bool _keepAwake;
    /// <summary>Keeps the output device open and fed with an inaudible signal (±1 LSB), so HDMI
    /// outputs and TVs that sleep in silence do not cut off the start of the next sound.</summary>
    public bool KeepAwake
    {
        get => _keepAwake;
        set { _keepAwake = value; if (value) EnsureRunning(); }
    }

    /// <summary>Output devices: (waveOut id, name). Names are cut at 31 characters by Windows.</summary>
    public static List<(uint Id, string Name)> Devices()
    {
        var list = new List<(uint, string)>();
        uint n = waveOutGetNumDevs();
        for (uint i = 0; i < n; i++)
        {
            var caps = new WAVEOUTCAPS();
            if (waveOutGetDevCapsW((UIntPtr)i, ref caps, (uint)Marshal.SizeOf<WAVEOUTCAPS>()) == 0) list.Add((i, caps.szPname));
        }
        return list;
    }

    /// <summary>Chooses the output by name; null or an unknown name = the default device.</summary>
    public void SetDevice(string? name)
    {
        uint id = WaveMapper;
        if (!string.IsNullOrEmpty(name))
            foreach (var d in Devices()) if (d.Name == name) { id = d.Id; break; }
        if (id == _device) return;
        lock (_lock) { _device = id; }
        RestartIfPlaying();
    }

    /// <summary>Starts a sound; returns a handle for <see cref="Stop"/> (loops until stopped).</summary>
    public object? Play(Sound? sound, bool loop = false, float gain = 1f)
    {
        if (sound == null) return null;
        var v = new Voice { Sound = sound, Loop = loop, Gain = Math.Clamp(gain, 0f, 10f) };
        lock (_lock) _voices.Add(v);
        EnsureRunning();
        return v;
    }

    /// <summary>Changes the volume of a playing sound (0–1), e.g. the looping Typing.</summary>
    public void SetGain(object? handle, float gain)
    {
        if (handle is Voice v) v.Gain = Math.Clamp(gain, 0f, 10f);
    }

    public void Stop(object? handle)
    {
        if (handle is Voice v) lock (_lock) _voices.Remove(v);
    }

    private void RestartIfPlaying()
    {
        bool playing;
        lock (_lock) playing = _voices.Count > 0 || _keepAwake;
        if (!playing || _thread == null) return;
        _stop = true; _thread.Join(1000); _thread = null; _stop = false;
        EnsureRunning();
    }

    private void EnsureRunning()
    {
        if (_thread is { IsAlive: true }) return;
        _stop = false;
        _thread = new Thread(Run) { IsBackground = true, Name = "audio" };
        _thread.Start();
    }

    private void Run()
    {
        var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 2, nSamplesPerSec = Rate, nAvgBytesPerSec = Rate * 4, nBlockAlign = 4, wBitsPerSample = 16 };
        using var ready = new AutoResetEvent(false);
        uint device; lock (_lock) device = _device;
        if (waveOutOpen(out IntPtr wave, (UIntPtr)device, ref fmt, ready.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT) != 0)
        {
            lock (_lock) _voices.Clear(); // the device is gone: drop what was asked, do not spin
            return;
        }

        int bytes = Frames * 4, headerSize = Marshal.SizeOf<WAVEHDR>();
        var headers = new IntPtr[Buffers];
        var mix = new int[Frames * 2];
        var pcm = new short[Frames * 2];
        int idleBuffers = 0;
        try
        {
            for (int i = 0; i < Buffers; i++)
            {
                headers[i] = Marshal.AllocHGlobal(headerSize);
                var h = new WAVEHDR { lpData = Marshal.AllocHGlobal(bytes), dwBufferLength = (uint)bytes };
                Marshal.StructureToPtr(h, headers[i], false);
                waveOutPrepareHeader(wave, headers[i], (uint)headerSize);
                Fill(headers[i], mix, pcm, ref idleBuffers);
                waveOutWrite(wave, headers[i], (uint)headerSize);
            }
            while (!_stop)
            {
                ready.WaitOne(100);
                for (int i = 0; i < Buffers; i++)
                {
                    var h = Marshal.PtrToStructure<WAVEHDR>(headers[i]);
                    if ((h.dwFlags & WHDR_DONE) == 0) continue;
                    // Close the device after a second of silence; Play opens it again.
                    if (!_keepAwake && idleBuffers > Buffers * 6) { _stop = true; break; }
                    Fill(headers[i], mix, pcm, ref idleBuffers);
                    waveOutWrite(wave, headers[i], (uint)headerSize);
                }
            }
        }
        finally
        {
            waveOutReset(wave);
            foreach (var hp in headers)
            {
                if (hp == IntPtr.Zero) continue;
                waveOutUnprepareHeader(wave, hp, (uint)headerSize);
                Marshal.FreeHGlobal(Marshal.PtrToStructure<WAVEHDR>(hp).lpData);
                Marshal.FreeHGlobal(hp);
            }
            waveOutClose(wave);
            _thread = null;
            lock (_lock) if (_voices.Count > 0 && !_disposed) ThreadPool.QueueUserWorkItem(_ => EnsureRunning());
        }
    }

    private void Fill(IntPtr header, int[] mix, short[] pcm, ref int idleBuffers)
    {
        Array.Clear(mix);
        bool any;
        lock (_lock)
        {
            any = _voices.Count > 0;
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                var voice = _voices[v];
                var s = voice.Sound.Samples;
                float g = voice.Gain;
                for (int i = 0; i < mix.Length; i++)
                {
                    if (voice.Pos >= s.Length)
                    {
                        if (!voice.Loop) break;
                        voice.Pos = 0;
                    }
                    mix[i] += (int)(s[voice.Pos++] * g);
                }
                if (!voice.Loop && voice.Pos >= s.Length) _voices.RemoveAt(v);
            }
        }
        idleBuffers = any ? 0 : idleBuffers + 1;
        float vol = _volume;
        for (int i = 0; i < mix.Length; i++) pcm[i] = (short)Math.Clamp(mix[i] * vol, -32768f, 32767f);
        if (!any && _keepAwake)
            for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)((i & 2) == 0 ? 1 : -1); // -90 dB: not silence to the device, nothing to the ear
        var h = Marshal.PtrToStructure<WAVEHDR>(header);
        Marshal.Copy(pcm, 0, h.lpData, pcm.Length);
    }

    private bool _disposed;
    public void Dispose()
    {
        _disposed = true;
        lock (_lock) _voices.Clear();
        _stop = true;
        _thread?.Join(1000);
    }

    // ------------------------------------------------------------------ winmm

    private const uint CALLBACK_EVENT = 0x00050000;
    private const uint WHDR_DONE = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag, nChannels;
        public uint nSamplesPerSec, nAvgBytesPerSec;
        public ushort nBlockAlign, wBitsPerSample, cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength, dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags, dwLoops;
        public IntPtr lpNext, reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WAVEOUTCAPS
    {
        public ushort wMid, wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
        public uint dwFormats;
        public ushort wChannels, wReserved1;
        public uint dwSupport;
    }

    [DllImport("winmm.dll")] private static extern uint waveOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern int waveOutGetDevCapsW(UIntPtr id, ref WAVEOUTCAPS caps, uint size);
    [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr wave, UIntPtr device, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr wave, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr wave, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr wave, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr wave);
    [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr wave);
}
