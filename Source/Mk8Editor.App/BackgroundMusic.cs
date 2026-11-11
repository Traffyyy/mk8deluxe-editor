using System;
using System.IO;
using System.Windows.Media;

namespace Mk8Editor.App
{
    /// <summary>Plays the bundled WAV locally. Turning audio off pauses it; on resumes it.</summary>
    public sealed class BackgroundMusic : IDisposable
    {
        private readonly MediaPlayer player = new MediaPlayer();
        private bool opened;
        private bool disposed;
        private readonly bool silentVerification;
        public const string TrackTitle = "˙✧˖° ༘ ⋆｡˚🗝˙ - Traffy, Meminz Feat Dyspony";
        public double Volume { get; private set; }
        internal double OutputVolume { get { return player.Volume; } }
        public bool Enabled { get; private set; }
        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        public TimeSpan Position { get { return player.Position; } }
        public TimeSpan Duration { get { return player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan : TimeSpan.Zero; } }
        public event Action StateChanged;
        public BackgroundMusic(bool silentVerification)
        {
            Enabled = true;
            this.silentVerification = silentVerification;
            Volume = 0.35;
            player.Volume = silentVerification ? 0 : Volume;
            player.MediaOpened += delegate {
                IsReady = true; Error = null;
                if (Enabled) player.Play();
                Notify();
            };
            player.MediaEnded += delegate {
                player.Position = TimeSpan.Zero;
                if (Enabled) player.Play();
            };
            player.MediaFailed += delegate {
                Error = "Unable to play the bundled WAV file. Check that the Audio folder is next to the application.";
                IsReady = false; Notify();
            };
        }
        public void Start()
        {
            if (disposed || opened) return;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio", "reference.wav");
            if (!File.Exists(path))
            {
                Error = "Audio/reference.wav is missing. Extract the complete application package.";
                Notify(); return;
            }
            try { opened = true; player.Open(new Uri(path, UriKind.Absolute)); }
            catch (Exception) { opened = false; Error = "Unable to open Audio/reference.wav."; Notify(); }
        }
        public void Toggle()
        {
            if (disposed) return;
            Enabled = !Enabled;
            if (IsReady) { if (Enabled) player.Play(); else player.Pause(); }
            Notify();
        }
        public void SetVolume(double value)
        {
            if (disposed) return;
            if (Double.IsNaN(value) || Double.IsInfinity(value)) throw new ArgumentOutOfRangeException("value");
            Volume = Math.Max(0, Math.Min(1, value));
            player.Volume = silentVerification ? 0 : Volume;
            Notify();
        }
        private void Notify() { if (StateChanged != null) StateChanged(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; player.Stop(); player.Close();
        }
    }
}
