using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Island.Tabs;

public partial class ControlsTab : UserControl
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _syncing;
    private int _pendingBrightness;
    private readonly DispatcherTimer _brightnessTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };

    public ControlsTab()
    {
        InitializeComponent();
        // WMI is slow, so brightness writes are throttled and run off the UI thread.
        _brightnessTimer.Tick += (_, _) =>
        {
            _brightnessTimer.Stop();
            int value = _pendingBrightness;
            Task.Run(() => Brightness.Set(value));
        };
        Loaded += async (_, _) => await InitMediaAsync();
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) RefreshLevels(); };
    }

    // ---------- media ----------

    private async Task InitMediaAsync()
    {
        if (_manager != null) return;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
            _manager.CurrentSessionChanged += (_, _) => Dispatcher.InvokeAsync(AttachSession);
            AttachSession();
        }
        catch { }
    }

    private void AttachSession()
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        _session = _manager?.GetCurrentSession();
        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        }
        _ = RefreshMediaAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) =>
        Dispatcher.InvokeAsync(() => _ = RefreshMediaAsync());

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) =>
        Dispatcher.InvokeAsync(UpdatePlayState);

    private async Task RefreshMediaAsync()
    {
        var session = _session;
        if (session == null)
        {
            SongTitle.Text = "재생 중인 음악 없음";
            SongArtist.Text = "";
            Art.ImageSource = null;
            UpdatePlayState();
            return;
        }
        try
        {
            var props = await session.TryGetMediaPropertiesAsync().AsTask();
            if (session != _session) return;
            var title = string.IsNullOrEmpty(props.Title) ? "알 수 없는 제목" : props.Title;
            bool changed = title != SongTitle.Text;
            Anim.SwapText(SongTitle, title);
            Anim.SwapText(SongArtist, props.Artist ?? "");
            var art = await LoadThumbnailAsync(props.Thumbnail);
            if (session != _session) return;
            Art.ImageSource = art;
            if (changed)
            {
                ArtFrame.Opacity = 0;
                Anim.To(ArtFrame, OpacityProperty, 1, 350);
            }
        }
        catch { }
        UpdatePlayState();
    }

    private static async Task<ImageSource?> LoadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference == null) return null;
        try
        {
            using var winStream = await reference.OpenReadAsync().AsTask();
            using var stream = winStream.AsStreamForRead();
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 144;
            bitmap.StreamSource = memory;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private void UpdatePlayState()
    {
        bool playing = _session?.GetPlaybackInfo()?.PlaybackStatus ==
                       GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        PlayButton.Content = playing ? "" : "";
    }

    private void Play_Click(object sender, RoutedEventArgs e) => _ = _session?.TryTogglePlayPauseAsync();
    private void Prev_Click(object sender, RoutedEventArgs e) => _ = _session?.TrySkipPreviousAsync();
    private void Next_Click(object sender, RoutedEventArgs e) => _ = _session?.TrySkipNextAsync();

    // ---------- volume / brightness ----------

    private async void RefreshLevels()
    {
        _syncing = true;
        if (AudioVolume.Get() is { } volume)
        {
            VolumeSlider.Value = Math.Round(volume.Level * 100);
            UpdateVolumeUi(volume.Muted);
        }
        _syncing = false;

        var brightness = await Task.Run(Brightness.Get);
        if (brightness is int b)
        {
            _syncing = true;
            BrightnessSlider.Value = b;
            BrightnessText.Text = b.ToString();
            _syncing = false;
            BrightnessRow.Visibility = Visibility.Visible;
        }
        else
        {
            BrightnessRow.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateVolumeUi(bool muted)
    {
        VolumeText.Text = ((int)VolumeSlider.Value).ToString();
        MuteButton.Content = muted ? "" : "";
        MuteButton.Foreground = muted ? new SolidColorBrush(Color.FromRgb(0xE5, 0x53, 0x4B)) : Brushes.White;
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || VolumeText == null) return;
        AudioVolume.SetLevel((float)(e.NewValue / 100));
        UpdateVolumeUi(AudioVolume.Get()?.Muted ?? false);
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        bool muted = !(AudioVolume.Get()?.Muted ?? false);
        AudioVolume.SetMute(muted);
        UpdateVolumeUi(muted);
    }

    private void Brightness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || BrightnessText == null) return;
        _pendingBrightness = (int)e.NewValue;
        BrightnessText.Text = _pendingBrightness.ToString();
        _brightnessTimer.Stop();
        _brightnessTimer.Start();
    }
}
