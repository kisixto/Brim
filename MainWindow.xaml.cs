using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Brim;

public partial class MainWindow : Window
{
    private const double CollapsedWidth = 130, CollapsedHeight = 6, CollapsedRadius = 4;
    private const double ExpandedWidth = 540, ExpandedHeight = 200, ExpandedRadius = 24;

    private static readonly Color TabIdle = Color.FromRgb(0x8A, 0x8A, 0x8E);
    private static readonly Color TabHover = Colors.White;
    private static readonly Color TabActive = Color.FromRgb(0xE8, 0xC8, 0x78);

    // Bottom corner radius, animated alongside the size so the shape morphs instead of snapping.
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(double), typeof(MainWindow),
        new PropertyMetadata(CollapsedRadius, (d, _) => ((MainWindow)d).UpdateShape()));

    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    private readonly DispatcherTimer _expandTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly (string Name, FrameworkElement Page)[] _tabs;
    private readonly List<RadioButton> _tabButtons = new();
    private int _current = -1;
    private bool _expanded;

    public MainWindow()
    {
        InitializeComponent();

        _tabs = new (string, FrameworkElement)[]
        {
            ("Controls", ControlsPage), ("Today", TodayPage), ("Stats", StatsPage), ("Clipboard", ClipboardPage),
            ("Apps", AppsPage), ("Wallpaper", WallpaperPage), ("Power", PowerPage), ("Options", OptionsPage),
        };
        foreach (var (name, page) in _tabs)
        {
            int index = _tabButtons.Count;
            var button = new RadioButton
            {
                Content = name, GroupName = "tabs", Style = (Style)FindResource("TabPill"),
                Foreground = new SolidColorBrush(TabIdle),
            };
            button.Checked += (_, _) => Select(index);
            button.MouseEnter += (_, _) => UpdateTabColors();
            button.MouseLeave += (_, _) => UpdateTabColors();
            _tabButtons.Add(button);
            TabBar.Children.Add(button);
            page.RenderTransform = new TranslateTransform();
        }
        int last = Array.FindIndex(_tabs, t => t.Name == Settings.Current.LastTab);
        Select(last < 0 ? 0 : last);
        ApplyTabVisibility();
        ApplyOpenSpeed();

        _expandTimer.Tick += (_, _) => { _expandTimer.Stop(); Expand(); };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!CursorInsideExpandedArea()) Collapse();
        };
        Deactivated += (_, _) => { if (!CursorInsideExpandedArea()) Collapse(); };
        Body.SizeChanged += (_, _) => UpdateShape();

        Loaded += (_, _) =>
        {
            PlaceAtTop();
            Updater.Start();
        };
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.WorkArea)) PlaceAtTop();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        // Tool window: hidden from Alt+Tab.
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE) | Native.WS_EX_TOOLWINDOW);
        Native.AddClipboardFormatListener(hwnd);
        HwndSource.FromHwnd(hwnd)!.AddHook(WndProc);
        _hwnd = hwnd;
    }

    // ---------- capture ----------

    private IntPtr _hwnd;
    private int _hideRequests;
    private bool _recording;

    // The island shows up in other apps' screenshots and screen sharing like any window, but steps out
    // of the frame for its own captures. Counted, so a screenshot taken mid-recording doesn't unhide it.
    // Requests are counted even when "hide from own captures" is off, so toggling it mid-recording stays balanced.
    public void SetHiddenFromCapture(bool hidden)
    {
        _hideRequests = Math.Max(0, _hideRequests + (hidden ? 1 : -1));
        bool exclude = _hideRequests > 0 && Settings.Current.HideFromOwnCaptures;
        Native.SetWindowDisplayAffinity(_hwnd, exclude ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE);
    }

    // While recording, the collapsed bar glows red so it's obvious even with the island closed.
    public void SetRecording(bool recording)
    {
        _recording = recording;
        UpdateRecordGlow();
    }

    private void UpdateRecordGlow()
    {
        if (_recording && !_expanded)
        {
            RecordGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, Anim.Ms(900))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        }
        else
        {
            Anim.To(RecordGlow, OpacityProperty, 0, 200);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_CLIPBOARDUPDATE)
            Dispatcher.BeginInvoke(ClipboardPage.Capture);
        return IntPtr.Zero;
    }

    private void PlaceAtTop()
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
    }

    // ---------- tabs ----------

    private void Select(int index)
    {
        if (index == _current) return;
        int previous = _current;
        _current = index;

        for (int i = 0; i < _tabs.Length; i++)
            _tabs[i].Page.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        _tabButtons[index].IsChecked = true;

        if (_expanded && previous >= 0)
        {
            // New page drifts in from the side it was scrolled from.
            var page = _tabs[index].Page;
            page.Opacity = 0;
            Anim.To(page, OpacityProperty, 1, 220);
            var slide = (TranslateTransform)page.RenderTransform;
            slide.BeginAnimation(TranslateTransform.XProperty, null);
            slide.X = index > previous ? 16 : -16;
            Anim.To(slide, TranslateTransform.XProperty, 0, 320, Anim.OutQuart);
        }
        UpdateTabColors();
        MoveIndicator(_expanded);

        if (Settings.Current.LastTab != _tabs[index].Name)
        {
            Settings.Current.LastTab = _tabs[index].Name;
            Settings.Current.Save();
        }
    }

    private void UpdateTabColors()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            var color = i == _current ? TabActive : _tabButtons[i].IsMouseOver ? TabHover : TabIdle;
            Anim.Brush(_tabButtons[i], ForegroundProperty, color, 200);
        }
    }

    private void MoveIndicator(bool animate)
    {
        var button = _tabButtons[_current];
        if (button.ActualWidth == 0) return; // not laid out yet; Expand() places it
        double x = button.TranslatePoint(new Point(0, 0), TabBar).X;
        if (animate)
        {
            Anim.To(TabIndicatorX, TranslateTransform.XProperty, x, 380, Anim.Spring);
            Anim.To(TabIndicator, WidthProperty, button.ActualWidth, 300);
        }
        else
        {
            TabIndicatorX.BeginAnimation(TranslateTransform.XProperty, null);
            TabIndicator.BeginAnimation(WidthProperty, null);
            TabIndicatorX.X = x;
            TabIndicator.Width = button.ActualWidth;
        }
    }

    private bool IsTabShown(int i) => _tabButtons[i].Visibility == Visibility.Visible;

    private void TabBar_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        int step = e.Delta < 0 ? 1 : -1, next = _current;
        do next = (next + step + _tabs.Length) % _tabs.Length; while (!IsTabShown(next));
        Select(next);
        e.Handled = true;
    }

    // Tabs switched off in Options. Options itself can't be hidden, so there is always a way back.
    public void ApplyTabVisibility()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool hidden = _tabs[i].Page != OptionsPage && Settings.Current.HiddenTabs.Contains(_tabs[i].Name);
            _tabButtons[i].Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        }
        if (!IsTabShown(_current)) Select(Array.FindIndex(_tabButtons.ToArray(), b => b.Visibility == Visibility.Visible));
        if (_expanded)
        {
            TabBar.UpdateLayout();
            MoveIndicator(false);
        }
    }

    public IEnumerable<string> HideableTabs => _tabs.Where(t => t.Page != OptionsPage).Select(t => t.Name);

    // How long the cursor rests on the bar before it opens; a short delay keeps brushing past it from popping it open.
    public void ApplyOpenSpeed() =>
        _expandTimer.Interval = TimeSpan.FromMilliseconds(Settings.Current.OpenSpeed switch
        {
            "fast" => 1,
            "slow" => 350,
            _ => 120,
        });

    // ---------- expand / collapse ----------

    private void Body_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (!_expanded) _expandTimer.Start();
    }

    private void Body_MouseLeave(object sender, MouseEventArgs e)
    {
        _expandTimer.Stop();
        if (Body.ContextMenu?.IsOpen == true) return;
        // Keep it open while typing a to-do.
        if (IsActive && Keyboard.FocusedElement is TextBox) return;
        _collapseTimer.Start();
    }

    // WPF only raises MouseLeave/MouseEnter when the mouse moves, so a cursor that slipped out of the
    // island while it was still growing looks "outside" even after the island has grown under it.
    // Check the real cursor against the island's final footprint instead.
    private bool CursorInsideExpandedArea()
    {
        if (!Native.GetCursorPos(out var screen)) return false;
        var p = PointFromScreen(new Point(screen.X, screen.Y));
        var area = new Rect((ActualWidth - ExpandedWidth) / 2, 0, ExpandedWidth, ExpandedHeight);
        return area.Contains(p);
    }

    private void Body_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_expanded) return;
        Expand();
        e.Handled = true;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (!Body.IsMouseOver) _collapseTimer.Start();
    }

    private void Expand()
    {
        _expandTimer.Stop();
        _collapseTimer.Stop();
        if (_expanded) return;
        _expanded = true;
        UpdateRecordGlow();

        ExpandedView.Visibility = Visibility.Visible;
        ExpandedView.UpdateLayout();
        MoveIndicator(false);

        // Width and height use a gentle overshoot so the island "springs" open, height slightly behind width.
        Anim.To(Body, WidthProperty, ExpandedWidth, 460, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 });
        Anim.To(Body, HeightProperty, ExpandedHeight, 500, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 }, 30);
        Anim.To(this, RadiusProperty, ExpandedRadius, 420, Anim.OutQuart);

        // Content arrives once there's room for it: fade in while settling down into place.
        Anim.To(ExpandedView, OpacityProperty, 1, 260, delayMs: 140);
        ExpandedSlide.BeginAnimation(TranslateTransform.YProperty, null);
        ExpandedSlide.Y = -10;
        Anim.To(ExpandedSlide, TranslateTransform.YProperty, 0, 420, Anim.OutQuart, 110);
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        PowerPage.Disarm();
        UpdateRecordGlow();

        // Content leaves first, then the island tucks back into the edge.
        Anim.To(ExpandedView, OpacityProperty, 0, 130, completed: (_, _) =>
        {
            if (!_expanded) ExpandedView.Visibility = Visibility.Collapsed;
        });
        Anim.To(ExpandedSlide, TranslateTransform.YProperty, -8, 200);

        Anim.To(Body, WidthProperty, CollapsedWidth, 420, Anim.OutQuart, 50);
        Anim.To(Body, HeightProperty, CollapsedHeight, 380, Anim.OutQuart, 50);
        Anim.To(this, RadiusProperty, CollapsedRadius, 380, Anim.OutQuart, 50);
    }

    private void UpdateShape()
    {
        double w = Body.ActualWidth, h = Body.ActualHeight;
        double r = Math.Max(0, Math.Min(Radius, Math.Min(w / 2, h)));
        Body.CornerRadius = new CornerRadius(0, 0, r, r);

        // Clip children to the same shape so nothing pokes out of the rounded corners mid-animation.
        var shape = new StreamGeometry();
        using (var ctx = shape.Open())
        {
            ctx.BeginFigure(new Point(0, 0), true, true);
            ctx.LineTo(new Point(w, 0), false, false);
            ctx.LineTo(new Point(w, h - r), false, false);
            ctx.ArcTo(new Point(w - r, h), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
            ctx.LineTo(new Point(r, h), false, false);
            ctx.ArcTo(new Point(0, h - r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        }
        shape.Freeze();
        Body.Clip = shape;
    }

    // ---------- drag files onto the island to pin them in Apps ----------

    private void Body_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        Expand();
        Select(Array.FindIndex(_tabs, t => t.Page == AppsPage));
    }

    private void Body_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void Body_DragLeave(object sender, DragEventArgs e) => _collapseTimer.Start();

    private void Body_Drop(object sender, DragEventArgs e)
    {
        _collapseTimer.Stop();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            AppsPage.AddFiles(files);
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => Quit();

    // Finish writing an in-progress recording before quitting, so the video isn't left corrupt.
    public void Quit() => ControlsPage.StopRecording(then: () => Application.Current.Shutdown());

    // Same care as Quit: a running recording is finished before Brim closes for the update.
    public void RestartForUpdate() => ControlsPage.StopRecording(then: Updater.ApplyAndRestart);
}
