using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Brim.Tabs;

// Taskbar-style list of open windows. Click to switch (or minimize the one already in front),
// right-click to close. Kept current by Windows shell events forwarded from MainWindow.
public partial class WindowsTab : UserControl
{
    private static readonly Color Gold = Color.FromRgb(0xE8, 0xC8, 0x78);
    private static readonly Color Attention = Color.FromRgb(0xF0, 0x88, 0x3E);
    private static readonly Color Idle = Color.FromRgb(0x55, 0x55, 0x5A);

    private readonly int _ownPid = Environment.ProcessId;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(1.5) }; // picks up title changes
    private readonly DispatcherTimer _soon = new() { Interval = TimeSpan.FromMilliseconds(120) }; // coalesces bursts of events
    private readonly List<IntPtr> _order = new(); // first seen first, like the taskbar
    private readonly Dictionary<IntPtr, Tile> _tiles = new();
    private readonly HashSet<IntPtr> _flashing = new();
    private IntPtr _active; // the last window in front that isn't Brim

    public WindowsTab()
    {
        InitializeComponent();
        _active = TaskWindows.GetForegroundWindow();
        _refresh.Tick += (_, _) => Rebuild();
        _soon.Tick += (_, _) => { _soon.Stop(); Rebuild(); };
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { Rebuild(); _refresh.Start(); }
            else _refresh.Stop();
        };
        Loc.Changed += () => { foreach (var tile in _tiles.Values) tile.RefreshMenu(); };
    }

    // Called by MainWindow for every shell hook message (window created/destroyed/activated/flashing...).
    public void OnShellEvent(int code, IntPtr hwnd)
    {
        switch (code)
        {
            case TaskWindows.HSHELL_WINDOWACTIVATED:
            case TaskWindows.HSHELL_RUDEAPPACTIVATED:
                if (hwnd != IntPtr.Zero && !IsOwn(hwnd)) { _active = hwnd; _flashing.Remove(hwnd); }
                break;
            case TaskWindows.HSHELL_FLASH:
                if (hwnd != _active) _flashing.Add(hwnd); // the app wants attention (new message, finished download, ...)
                break;
            case TaskWindows.HSHELL_WINDOWDESTROYED:
                _flashing.Remove(hwnd);
                break;
        }
        if (IsVisible) { _soon.Stop(); _soon.Start(); }
    }

    private bool IsOwn(IntPtr hwnd)
    {
        var window = Window.GetWindow(this);
        return window != null && new System.Windows.Interop.WindowInteropHelper(window).Handle == hwnd;
    }

    private void Rebuild()
    {
        var windows = TaskWindows.List(_ownPid);
        var alive = windows.Select(w => w.Handle).ToHashSet();
        foreach (var gone in _order.Where(h => !alive.Contains(h)).ToList())
        {
            _order.Remove(gone);
            _tiles.Remove(gone);
            _flashing.Remove(gone);
        }

        Tiles.Children.Clear();
        foreach (var window in windows)
        {
            if (!_order.Contains(window.Handle)) _order.Add(window.Handle);
            if (!_tiles.TryGetValue(window.Handle, out var tile))
                _tiles[window.Handle] = tile = new Tile(this, window);
            tile.Update(window.Title, window.Handle == _active && !TaskWindows.IsIconic(window.Handle), _flashing.Contains(window.Handle));
        }
        foreach (var handle in _order) Tiles.Children.Add(_tiles[handle].Root);
        Empty.Visibility = _order.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TileClicked(IntPtr hwnd)
    {
        // Clicking the window that's already in front minimizes it, as on the taskbar.
        if (hwnd == _active && !TaskWindows.IsIconic(hwnd)) TaskWindows.Minimize(hwnd);
        else
        {
            TaskWindows.Activate(hwnd);
            _active = hwnd;
        }
        _flashing.Remove(hwnd);
        Rebuild();
    }

    // ---------- shortcut buttons ----------

    private void Desktop_Click(object sender, RoutedEventArgs e) => Native.PressKeys(Native.VK_LWIN, Native.VK_D);
    private void TaskView_Click(object sender, RoutedEventArgs e) => Native.PressKeys(Native.VK_LWIN, Native.VK_TAB);
    private void QuickSettings_Click(object sender, RoutedEventArgs e) => Native.PressKeys(Native.VK_LWIN, Native.VK_A);
    private void Notifications_Click(object sender, RoutedEventArgs e) => Native.PressKeys(Native.VK_LWIN, Native.VK_N);

    // ---------- one window ----------

    private sealed class Tile
    {
        public Button Root { get; }
        private readonly WindowsTab _owner;
        private readonly IntPtr _hwnd;
        private readonly TextBlock _title;
        private readonly Border _marker;
        private bool? _wasActive, _wasFlashing;

        public Tile(WindowsTab owner, TaskWindows.Info window)
        {
            _owner = owner;
            _hwnd = window.Handle;
            _title = new TextBlock
            {
                FontSize = 10, Width = 58, Margin = new Thickness(0, 5, 0, 0),
                TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
            };
            // Under each window: a dot when open, a gold bar when in front, a pulsing orange bar when flashing.
            _marker = new Border { Height = 3, Width = 6, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 4, 0, 0), Background = new SolidColorBrush(Idle) };

            var stack = new StackPanel();
            stack.Children.Add(new Image { Source = TaskWindows.IconOf(window), Width = 28, Height = 28 });
            stack.Children.Add(_title);
            stack.Children.Add(_marker);

            Root = new Button { Style = (Style)owner.FindResource("AppTile"), Content = stack };
            Root.Click += (_, _) => _owner.TileClicked(_hwnd);
            RefreshMenu();
        }

        public void RefreshMenu()
        {
            var close = new MenuItem { Header = Loc.T("WinClose") };
            close.Click += (_, _) => TaskWindows.Close(_hwnd);
            Root.ContextMenu = new ContextMenu { Items = { close } };
        }

        public void Update(string title, bool active, bool flashing)
        {
            _title.Text = title;
            Root.ToolTip = title;
            if (active == _wasActive && flashing == _wasFlashing) return;
            _wasActive = active;
            _wasFlashing = flashing;

            Anim.To(_marker, FrameworkElement.WidthProperty, active || flashing ? 18 : 6, 250);
            Anim.Brush(_marker, Border.BackgroundProperty, flashing ? Attention : active ? Gold : Idle, 250);
            if (flashing)
                _marker.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.3, Anim.Ms(600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            else
                Anim.To(_marker, UIElement.OpacityProperty, 1, 200);
        }
    }
}
