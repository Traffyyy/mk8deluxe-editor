using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Mk8Editor.App
{
    /// <summary>Owns the speaker button and hover volume panel without touching editor state.</summary>
    public sealed class AudioControls : IDisposable
    {
        private readonly Window window;
        private readonly BackgroundMusic music;
        private readonly Grid layout;
        private readonly Button button;
        private readonly Border panel;
        private readonly Slider slider;
        private readonly TextBlock volumeValue;
        private readonly Path waves;
        private readonly Path cross;
        private readonly TextBlock title;
        private readonly DispatcherTimer closeTimer;
        private bool keyboardInteraction;
        public bool VolumePanelVisible { get { return panel.Visibility == Visibility.Visible; } }

        public AudioControls(Window window, BackgroundMusic music)
        {
            this.window = window; this.music = music;
            layout = (Grid)window.FindName("RootLayout");
            button = (Button)window.FindName("AudioButton");
            panel = (Border)window.FindName("VolumePanel");
            slider = (Slider)window.FindName("VolumeSlider");
            volumeValue = (TextBlock)window.FindName("VolumeValue");
            waves = (Path)window.FindName("AudioWaves"); cross = (Path)window.FindName("AudioCross");
            title = (TextBlock)window.FindName("TrackTitle");
            // WPF's default Segoe fallback misses this Tibetan combining mark on some Windows systems.
            // Keep the exact Unicode title, selecting its installed script font explicitly.
            int tibetanMark = BackgroundMusic.TrackTitle.IndexOf('\u0F18');
            title.Inlines.Add(new Run(BackgroundMusic.TrackTitle.Substring(0, tibetanMark)));
            title.Inlines.Add(new Run("\u0F18") { FontFamily = new FontFamily("Microsoft Himalaya, Arial Unicode MS"), FontSize = 21 });
            title.Inlines.Add(new Run(BackgroundMusic.TrackTitle.Substring(tibetanMark + 1)));
            closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            closeTimer.Tick += delegate {
                closeTimer.Stop();
                bool keyboardActive = keyboardInteraction && (panel.IsKeyboardFocusWithin || button.IsKeyboardFocused);
                if (!button.IsMouseOver && !title.IsMouseOver && !panel.IsMouseOver && !panel.IsMouseCaptureWithin && !keyboardActive) HideVolume();
            };
            button.Click += delegate { music.Toggle(); };
            button.MouseEnter += delegate { ShowVolume(); };
            panel.MouseEnter += delegate { closeTimer.Stop(); };
            title.MouseEnter += delegate { closeTimer.Stop(); };
            title.MouseLeave += delegate { ScheduleClose(); };
            button.MouseLeave += delegate { ScheduleClose(); };
            panel.MouseLeave += delegate { ScheduleClose(); };
            button.PreviewMouseDown += delegate { keyboardInteraction = false; };
            panel.PreviewMouseDown += delegate { keyboardInteraction = false; };
            panel.LostMouseCapture += delegate { ScheduleClose(); };
            button.GotKeyboardFocus += delegate {
                keyboardInteraction = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
                if (keyboardInteraction) ShowVolume();
            };
            button.LostKeyboardFocus += delegate { ScheduleClose(); };
            panel.LostKeyboardFocus += delegate { ScheduleClose(); };
            button.PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.Key == Key.Down) { keyboardInteraction = true; ShowVolume(); slider.Focus(); args.Handled = true; }
            };
            slider.ValueChanged += delegate { music.SetVolume(slider.Value / 100); };
            window.Deactivated += delegate { HideVolume(); };
            window.SizeChanged += delegate { HideVolume(); };
            layout.LayoutUpdated += delegate { if (VolumePanelVisible) PositionPanel(); };
            window.PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.Key == Key.Escape && VolumePanelVisible)
                {
                    if (panel.IsKeyboardFocusWithin) button.Focus();
                    HideVolume(); args.Handled = true;
                }
            };
            music.StateChanged += Refresh;
            Refresh();
        }
        public void ShowVolume()
        {
            closeTimer.Stop();
            PositionPanel();
            panel.Visibility = Visibility.Visible;
        }
        private void PositionPanel()
        {
            Point anchor = button.TranslatePoint(new Point(0, button.ActualHeight), layout);
            Point titleBottom = title.TranslatePoint(new Point(0, title.ActualHeight), layout);
            double left = Math.Max(8, Math.Min(anchor.X, layout.ActualWidth - panel.Width - 8));
            var margin = new Thickness(left, Math.Max(anchor.Y, titleBottom.Y) + 6, 0, 0);
            if (panel.Margin != margin) panel.Margin = margin;
        }
        public void HideVolume() { closeTimer.Stop(); panel.Visibility = Visibility.Collapsed; }
        private void ScheduleClose() { closeTimer.Stop(); closeTimer.Start(); }
        private void Refresh()
        {
            bool on = music.Enabled && music.Error == null;
            // Keep the header height stable while showing the song only when audio is enabled.
            title.Visibility = on ? Visibility.Visible : Visibility.Hidden;
            var stateColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(on ? "#147D44" : "#BF303D"));
            stateColor.Freeze(); button.Background = stateColor; button.BorderBrush = stateColor;
            button.IsEnabled = music.Error == null;
            waves.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            cross.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(button, music.Error ?? (on ? "Turn audio off" : "Turn audio on"));
            AutomationProperties.SetHelpText(button, music.Error ?? "Hover to adjust volume. Press Down to focus the volume slider.");
            volumeValue.Text = Math.Round(music.Volume * 100) + "%";
            if (Math.Abs(slider.Value - music.Volume * 100) > 0.001) slider.Value = music.Volume * 100;
            slider.IsEnabled = music.Error == null;
        }
        public void Dispose() { closeTimer.Stop(); music.StateChanged -= Refresh; }
    }
}
