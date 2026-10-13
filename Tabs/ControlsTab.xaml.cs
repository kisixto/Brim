using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenRecorderLib;
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

    // ---------- screen capture ----------

    private static readonly Color RecordIdle = Color.FromRgb(0x2A, 0x2A, 0x2D);
    private static readonly Color RecordActive = Color.FromRgb(0xB3, 0x26, 0x26);

    private Recorder? _recorder;
    private readonly Stopwatch _recordClock = new();
    private DispatcherTimer? _recordTimer;
    private Action? _afterRecording;
    private string? _lastCapture;

    private static string CaptureFolder(Environment.SpecialFolder root, string name)
    {
        var folder = Path.Combine(Environment.GetFolderPath(root), name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string Stamp() => DateTime.Now.ToString("yyyyMMdd_HHmmss");

    private MainWindow? Island => Window.GetWindow(this) as MainWindow;

    // Both buttons hand off to Windows' own capture: Win+PrtScn for the full screen, and
    // Win+Shift+S for Snipping Tool's selection overlay. The island steps out of the frame
    // for a moment so it doesn't end up in the shot.
    private async Task WithIslandHiddenAsync(params ushort[] keys)
    {
        Island?.SetHiddenFromCapture(true);
        try
        {
            Native.PressKeys(keys);
            await Task.Delay(2000); // the capture/freeze happens within this window
        }
        finally
        {
            Island?.SetHiddenFromCapture(false);
        }
    }

    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var started = DateTime.Now.AddSeconds(-1);
        await WithIslandHiddenAsync(Native.VK_LWIN, Native.VK_SNAPSHOT);

        // Windows names the file itself; find it so the status line can open it.
        var saved = await Task.Run(() => NewestScreenshotSince(started));
        ShowCaptureStatus(saved != null ? "화면 전체 저장됨 · 클릭해서 보기" : "화면 전체를 캡처했어요", saved);
    }

    private async void RegionScreenshot_Click(object sender, RoutedEventArgs e)
    {
        ShowCaptureStatus("캡처 도구에서 영역을 고르면 클립보드에 복사돼요", null);
        await WithIslandHiddenAsync(Native.VK_LWIN, Native.VK_SHIFT, Native.VK_S);
    }

    private static string? NewestScreenshotSince(DateTime since)
    {
        var folder = Native.ScreenshotsFolder();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                var file = new DirectoryInfo(folder).EnumerateFiles("*.png")
                    .Where(f => f.CreationTime >= since)
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault();
                if (file != null) return file.FullName;
            }
            catch (IOException) { }
            System.Threading.Thread.Sleep(150);
        }
        return null;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder != null) { StopRecording(); return; }

        // Same folder the Xbox Game Bar uses.
        var path = Path.Combine(CaptureFolder(Environment.SpecialFolder.MyVideos, "Captures"), $"Island_{Stamp()}.mp4");
        RecordButton.IsEnabled = false;
        Anim.SwapText(RecordText, "준비 중…");
        Island?.SetHiddenFromCapture(true); // kept out of its own recording; restored in OnRecordingFinished

        try
        {
            // Main monitor plus whatever is playing through the speakers (no microphone).
            var options = RecorderOptions.DefaultMainMonitor;
            if (LoopbackAudioSource.Default is { } speakers)
            {
                options.AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = true,
                    AudioSources = new System.Collections.Generic.List<AudioSourceBase> { speakers },
                };
            }
            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnStatusChanged += (_, args) => Dispatcher.InvokeAsync(() =>
            {
                if (args.Status == RecorderStatus.Recording) OnRecordingStarted();
            });
            _recorder.OnRecordingComplete += (_, args) => Dispatcher.InvokeAsync(() => OnRecordingFinished(args.FilePath, null));
            _recorder.OnRecordingFailed += (_, args) => Dispatcher.InvokeAsync(() => OnRecordingFinished(null, args.Error));
            _recorder.Record(path);
        }
        catch (Exception ex)
        {
            OnRecordingFinished(null, ex.Message);
        }
    }

    public bool IsRecording => _recorder != null;

    // Stops an in-progress recording; `then` runs once the file has been written (or right away if idle).
    public void StopRecording(Action? then = null)
    {
        if (_recorder == null) { then?.Invoke(); return; }
        _afterRecording = then;
        RecordButton.IsEnabled = false;
        Anim.SwapText(RecordText, "저장 중…");
        _recorder.Stop();
    }

    private void OnRecordingStarted()
    {
        RecordButton.IsEnabled = true;
        _recordClock.Restart();
        _recordTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _recordTimer.Tick += (_, _) => RecordText.Text = _recordClock.Elapsed.ToString(@"m\:ss");
        _recordTimer.Start();

        Anim.Brush(RecordButton, BackgroundProperty, RecordActive, 260);
        Anim.Brush(RecordIcon, TextBlock.ForegroundProperty, Colors.White, 260);
        RecordIcon.Text = ""; // stop
        Anim.SwapText(RecordText, "0:00");
        Island?.SetRecording(true);
    }

    private void OnRecordingFinished(string? path, string? error)
    {
        _recordTimer?.Stop();
        _recordClock.Reset();
        (_recorder as IDisposable)?.Dispose();
        _recorder = null;

        RecordButton.IsEnabled = true;
        Anim.Brush(RecordButton, BackgroundProperty, RecordIdle, 300);
        Anim.Brush(RecordIcon, TextBlock.ForegroundProperty, Color.FromRgb(0xE5, 0x48, 0x4D), 300);
        RecordIcon.Text = ""; // record dot
        Anim.SwapText(RecordText, "녹화");
        Island?.SetRecording(false);
        Island?.SetHiddenFromCapture(false);

        if (error != null) ShowCaptureStatus($"녹화 실패: {error}", null);
        else if (path != null) ShowCaptureStatus("녹화 저장됨 · 클릭해서 보기", path);

        var then = _afterRecording;
        _afterRecording = null;
        then?.Invoke();
    }

    private void ShowCaptureStatus(string text, string? path)
    {
        _lastCapture = path;
        CaptureStatusText.Text = text;
        CaptureStatus.IsHitTestVisible = path != null;
        CaptureStatus.Opacity = 0;
        Anim.To(CaptureStatus, OpacityProperty, 1, 250);
    }

    // Opens Explorer with the saved file selected.
    private void CaptureStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_lastCapture == null || !File.Exists(_lastCapture)) return;
        Process.Start("explorer.exe", $"/select,\"{_lastCapture}\"");
    }

    // ---------- media ----------

    private async Task InitMediaAsync()
    {
        if (_manager != null) return;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
            _manager.CurrentSessionChanged += (_, _) => Dispatcher.InvokeAsync(AttachSession);
            _manager.SessionsChanged += (_, _) => Dispatcher.InvokeAsync(AttachSession);
            AttachSession();
        }
        catch { }
    }

    // The session the user picked from the pills. Without a pick, the panel follows whatever
    // Windows considers current (usually the last thing that started playing).
    private GlobalSystemMediaTransportControlsSession? _pinned;

    private void AttachSession()
    {
        var sessions = _manager?.GetSessions() ?? (IReadOnlyList<GlobalSystemMediaTransportControlsSession>)Array.Empty<GlobalSystemMediaTransportControlsSession>();
        if (_pinned != null && !sessions.Any(s => SameSession(s, _pinned))) _pinned = null;
        var target = _pinned != null ? sessions.First(s => SameSession(s, _pinned)) : _manager?.GetCurrentSession();

        if (!SameSession(target, _session))
        {
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            _session = target;
            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }
            _ = RefreshMediaAsync();
        }
        UpdateSessionPills(sessions);
    }

    // Windows hands out a fresh wrapper object on every GetSessions()/GetCurrentSession() call, so
    // compare by app ID. (Browsers report all their tabs as one session, so one ID = one session.)
    private static bool SameSession(GlobalSystemMediaTransportControlsSession? a, GlobalSystemMediaTransportControlsSession? b) =>
        ReferenceEquals(a, b) || (a != null && b != null && a.SourceAppUserModelId == b.SourceAppUserModelId);

    private void UpdateSessionPills(IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions)
    {
        SessionPills.Children.Clear();
        SessionPills.Visibility = sessions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (sessions.Count <= 1) return;

        var names = sessions.Select(s => AppName(s.SourceAppUserModelId)).ToList();
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            // Two tabs of the same browser get numbered so they can be told apart.
            var label = names.Count(n => n == names[i]) > 1 ? $"{names[i]} {names.Take(i + 1).Count(n => n == names[i])}" : names[i];
            bool selected = SameSession(session, _session);

            var pill = new Button
            {
                Style = (Style)FindResource("TextButton"),
                Content = label,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(0),
            };
            pill.Template = PillTemplate(selected);
            pill.Foreground = new SolidColorBrush(selected ? Colors.White : Color.FromRgb(0x8A, 0x8A, 0x8E));
            pill.Click += (_, _) =>
            {
                _pinned = session;
                AttachSession();
            };
            _ = SetPillTooltipAsync(pill, session);
            SessionPills.Children.Add(pill);
        }
    }

    private static ControlTemplate PillTemplate(bool selected)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.PaddingProperty, new Thickness(8, 2, 8, 2));
        border.SetValue(Border.BackgroundProperty, selected ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)) : Brushes.Transparent);
        border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static async Task SetPillTooltipAsync(Button pill, GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var props = await session.TryGetMediaPropertiesAsync().AsTask();
            pill.ToolTip = string.IsNullOrEmpty(props.Artist) ? props.Title : $"{props.Title} · {props.Artist}";
        }
        catch { }
    }

    private static readonly Dictionary<string, string> AppNames = new();

    // Session IDs are app IDs: "chrome.exe", "SpotifyAB.SpotifyMusic_…!Spotify", or an opaque hash
    // such as "F0DC299D809B9700" (Zen). The Start menu's app list knows the friendly name for all of them.
    private static string AppName(string id)
    {
        if (AppNames.TryGetValue(id, out var cached)) return cached;
        string? name = null;
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            name = (string?)shell.NameSpace("shell:AppsFolder").ParseName(id)?.Name;
        }
        catch { }
        return AppNames[id] = string.IsNullOrWhiteSpace(name) ? AppNameFromId(id) : name!;
    }

    private static string AppNameFromId(string id)
    {
        var name = id.Contains('!') ? id[(id.LastIndexOf('!') + 1)..] : id;
        name = Path.GetFileNameWithoutExtension(name);
        return name.ToLowerInvariant() switch
        {
            "msedge" or "microsoftedge" => "Edge",
            "chrome" => "Chrome",
            "firefox" => "Firefox",
            "whale" => "Whale",
            "app" when id.Contains("Spotify", StringComparison.OrdinalIgnoreCase) => "Spotify",
            _ => name.Length > 0 ? char.ToUpper(name[0]) + name[1..] : "미디어",
        };
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
