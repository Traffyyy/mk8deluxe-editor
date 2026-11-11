using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Mk8Editor.Desktop;

/// <summary>Owns an SDL playback stream. Audio failure never prevents editing.</summary>
public sealed unsafe class PortableAudio : IDisposable
{
    public const string TrackTitle = "˙✧˖° ༘ ⋆｡˚🗝˙ - Traffy, Meminz Feat Dyspony";
    private readonly object gate = new();
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private SDL_AudioStream* stream;
    private byte* wave;
    private uint waveLength;
    private int position, chunkBytes;
    private Timer? refill;
    private bool initialized, disposed, silent;
    public bool Enabled { get; private set; }
    public double Volume { get; private set; } = .35;
    public bool IsReady { get; private set; }
    public string? Error { get; private set; }
    public event Action? StateChanged;

    public void Start(bool silent = false)
    {
        lock (gate)
        {
            if (disposed || IsReady) return;
            this.silent = silent;
            try
            {
                if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO)) throw AudioError();
                initialized = true;
                SDL_AudioSpec spec;
                byte* loadedWave = null;
                uint loadedLength = 0;
                string path = Path.Combine(AppContext.BaseDirectory, "Audio", "reference.wav");
                if (!File.Exists(path)) throw new IOException("The music file is missing.");
                Utf8String pathUtf8 = path;
                    if (!SDL_LoadWAV(pathUtf8, &spec, &loadedWave, &loadedLength)) throw AudioError();
                wave = loadedWave; waveLength = loadedLength;
                if (waveLength == 0 || waveLength > int.MaxValue) throw new IOException("The music file is empty or too large.");
                int frameBytes = ((int)spec.format & 255) / 8 * spec.channels;
                if (frameBytes <= 0 || spec.freq <= 0) throw new IOException("Unsupported music format.");
                chunkBytes = checked(spec.freq * frameBytes);
                position = 0;
                stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, null, IntPtr.Zero);
                if (stream == null) throw AudioError();
                if (!SDL_SetAudioStreamGain(stream, silent ? 0 : (float)Volume)) throw AudioError();
                FillQueue();
                if (!SDL_ResumeAudioStreamDevice(stream)) throw AudioError();
                Enabled = true; IsReady = true; Error = null;
                refill = new Timer(_ => Refill(), null, 200, 200);
            }
            catch (Exception error) { Fail(error); }
        }
        Notify();
    }

    public void Toggle()
    {
        if (!IsReady) { Start(silent); return; }
        lock (gate)
        {
            if (disposed || stream == null) return;
            try
            {
                bool result = Enabled ? SDL_PauseAudioStreamDevice(stream) : SDL_ResumeAudioStreamDevice(stream);
                if (!result) throw AudioError();
                Enabled = !Enabled;
            }
            catch (Exception error) { Fail(error); }
        }
        Notify();
    }

    public void SetVolume(double volume)
    {
        if (!double.IsFinite(volume)) throw new ArgumentOutOfRangeException(nameof(volume));
        lock (gate)
        {
            Volume = Math.Clamp(volume, 0, 1);
            if (!disposed && stream != null && !SDL_SetAudioStreamGain(stream, silent ? 0 : (float)Volume))
                Fail(AudioError());
        }
        Notify();
    }

    private void Refill()
    {
        bool failed = false;
        lock (gate)
        {
            if (disposed || !IsReady || !Enabled || stream == null) return;
            try { FillQueue(); }
            catch (Exception error) { Fail(error); failed = true; }
        }
        if (failed) Notify();
    }

    private void FillQueue()
    {
        int queued = SDL_GetAudioStreamQueued(stream);
        if (queued < 0) throw AudioError();
        // Maintain two seconds of input; append the start before the end is consumed.
        while (queued < chunkBytes * 2)
        {
            int count = Math.Min(chunkBytes, (int)waveLength - position);
            if (!SDL_PutAudioStreamData(stream, (IntPtr)(wave + position), count)) throw AudioError();
            position = (position + count) % (int)waveLength;
            queued += count;
        }
    }

    private static IOException AudioError() => new("Audio unavailable: " + SDL_GetError());
    private void Fail(Exception error)
    {
        Error = error.Message;
        Release();
    }
    private void Release()
    {
        refill?.Dispose(); refill = null;
        if (stream != null) { SDL_DestroyAudioStream(stream); stream = null; }
        if (wave != null) { SDL_free(wave); wave = null; }
        if (initialized) { SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO); initialized = false; }
        IsReady = false; Enabled = false;
    }
    private void Notify()
    {
        if (context != null && SynchronizationContext.Current != context) context.Post(_ => StateChanged?.Invoke(), null);
        else StateChanged?.Invoke();
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; Release(); }
        GC.SuppressFinalize(this);
    }
}

