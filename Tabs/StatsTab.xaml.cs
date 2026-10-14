using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brim.Tabs;

public partial class StatsTab : UserControl
{
    private const int TopCount = 5;
    private static readonly Color Gold = Color.FromRgb(0xE8, 0xC8, 0x78);
    private static readonly Color CardEdge = Color.FromRgb(0x2A, 0x2A, 0x2D);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Tile _cpu, _ram, _disk, _power;
    private readonly List<TopRow> _rows = new();
    private readonly ProcessSampler _sampler = new();
    private readonly Dictionary<string, (string Name, ImageSource? Icon)> _appInfo = new(StringComparer.OrdinalIgnoreCase);
    private long _lastIdle, _lastTotal;
    private bool _sampling;

    private static Settings S => Settings.Current;
    private bool FocusCpu => S.StatsFocus != "memory";

    public StatsTab()
    {
        InitializeComponent();
        _cpu = AddTile("CPU", "cpu");
        _ram = AddTile("Memory", "memory");
        _disk = AddTile("Disk", null);
        _power = AddTile("Battery", null);
        for (int i = 0; i < TopCount; i++)
        {
            var row = new TopRow(this);
            _rows.Add(row);
            TopRows.Children.Add(row.Root);
        }
        ShowFocus();

        _timer.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { SampleCpu(); Refresh(); _timer.Start(); }
            else _timer.Stop();
        };
        Loc.Changed += ShowFocus;
    }

    // focusKey: which top-apps list clicking this tile shows (null = not clickable).
    private Tile AddTile(string labelKey, string? focusKey)
    {
        var tile = new Tile(Loc.T(labelKey), (Style)FindResource("Card"));
        if (labelKey != "CPU") tile.Label.SetResourceReference(TextBlock.TextProperty, "L." + labelKey);
        if (focusKey != null)
        {
            tile.Root.Cursor = Cursors.Hand;
            tile.Root.ToolTip = Loc.T("StatsPick");
            tile.Root.MouseLeftButtonUp += (_, _) =>
            {
                if (S.StatsFocus == focusKey) return;
                S.StatsFocus = focusKey;
                S.Save();
                ShowFocus();
                _ = RefreshTopAsync();
            };
        }
        Tiles.Children.Add(tile.Root);
        return tile;
    }

    private void ShowFocus()
    {
        TopTitle.Text = Loc.T(FocusCpu ? "TopCpu" : "TopMemory");
        Anim.Brush(_cpu.Root, Border.BorderBrushProperty, FocusCpu ? Gold : CardEdge, 250);
        Anim.Brush(_ram.Root, Border.BorderBrushProperty, FocusCpu ? CardEdge : Gold, 250);
    }

    private double SampleCpu()
    {
        Native.GetSystemTimes(out long idle, out long kernel, out long user);
        long total = kernel + user; // kernel time includes idle time
        long dIdle = idle - _lastIdle, dTotal = total - _lastTotal;
        _lastIdle = idle;
        _lastTotal = total;
        return dTotal <= 0 ? 0 : 1 - (double)dIdle / dTotal;
    }

    private void Refresh()
    {
        double cpu = SampleCpu();
        _cpu.Set(cpu, $"{cpu * 100:0}%", Loc.T("Threads", Environment.ProcessorCount));

        var mem = new Native.MemoryStatus { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MemoryStatus>() };
        Native.GlobalMemoryStatusEx(ref mem);
        double usedGb = (mem.TotalPhys - mem.AvailPhys) / 1073741824.0, totalGb = mem.TotalPhys / 1073741824.0;
        _ram.Set(mem.MemoryLoad / 100.0, $"{mem.MemoryLoad}%", $"{usedGb:0.0} / {totalGb:0} GB");

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            double used = 1 - (double)drive.AvailableFreeSpace / drive.TotalSize;
            _disk.Set(used, $"{used * 100:0}%", Loc.T("DiskFree", drive.Name.TrimEnd('\\'), drive.AvailableFreeSpace / 1073741824.0));
        }
        catch { }

        Native.GetSystemPowerStatus(out var power);
        if ((power.BatteryFlag & 128) == 0 && power.BatteryLifePercent <= 100)
        {
            _power.Label.SetResourceReference(TextBlock.TextProperty, "L.Battery");
            _power.Set(power.BatteryLifePercent / 100.0, $"{power.BatteryLifePercent}%",
                       Loc.T(power.ACLineStatus == 1 ? "Charging" : "OnBattery"), invert: true);
        }
        else
        {
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            _power.Label.SetResourceReference(TextBlock.TextProperty, "L.Uptime");
            _power.Set(null, up.TotalDays >= 1 ? Loc.T("Days", (int)up.TotalDays) : Loc.T("Hours", up.Hours),
                       up.TotalDays >= 1 ? Loc.T("HoursMinutes", up.Hours, up.Minutes) : Loc.T("Minutes", up.Minutes));
        }

        // Scanning every process is the costly part, so the top-apps list refreshes every other second.
        if (++_ticks % 2 == 1) _ = RefreshTopAsync();
    }

    private int _ticks;

    // ---------- top apps ----------

    private async Task RefreshTopAsync()
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            var apps = await Task.Run(_sampler.Sample); // ~300 processes; keep it off the UI thread
            bool cpu = FocusCpu;
            if (cpu && !_sampler.HasCpu)
            {
                for (int i = 0; i < _rows.Count; i++) _rows[i].Set(null, i == 0 ? Loc.T("TopMeasuring") : "", "", 0);
                return;
            }

            var top = (cpu ? apps.OrderByDescending(a => a.Cpu) : apps.OrderByDescending(a => a.Memory)).Take(TopCount).ToList();
            double max = top.Count == 0 ? 1 : Math.Max(cpu ? top[0].Cpu : top[0].Memory, 1e-9);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i >= top.Count) { _rows[i].Set(null, "", "", 0); continue; }
                var app = top[i];
                var (name, icon) = Describe(app);
                string value = cpu ? $"{app.Cpu * 100:0.0}%" : FormatBytes(app.Memory);
                _rows[i].Set(icon, name, value, (cpu ? app.Cpu : app.Memory) / max);
            }
        }
        finally { _sampling = false; }
    }

    // Friendly name ("Google Chrome") and icon from the exe, cached per process name.
    private (string Name, ImageSource? Icon) Describe(AppUsage app)
    {
        if (_appInfo.TryGetValue(app.Name, out var known)) return known;
        string name = app.Name;
        ImageSource? icon = null;
        var path = Native.ProcessPath(app.Pid);
        if (path != null)
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(description)) name = description.Trim();
            }
            catch { }
            icon = Native.GetFileInfo(path).Icon;
        }
        return _appInfo[app.Name] = (name, icon);
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : $"{bytes / (double)(1L << 20):0} MB";

    private sealed class TopRow
    {
        public Grid Root { get; }
        private readonly Image _icon = new() { Width = 16, Height = 16, Margin = new Thickness(6, 0, 8, 0) };
        private readonly TextBlock _name = new() { Foreground = Brushes.White, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _value = new() { Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E)), FontSize = 11, Margin = new Thickness(8, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly ScaleTransform _scale = new(0, 1);

        public TopRow(FrameworkElement owner)
        {
            _value.SetValue(System.Windows.Documents.Typography.NumeralAlignmentProperty, FontNumeralAlignment.Tabular);
            // A soft bar behind the row shows each app's share relative to the busiest one.
            var bar = new Border
            {
                CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.FromArgb(0x30, 0xE8, 0xC8, 0x78)),
                RenderTransform = _scale, IsHitTestVisible = false,
            };
            Root = new Grid { Height = 21, Margin = new Thickness(0, 0, 0, 1) };
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumnSpan(bar, 3);
            Grid.SetColumn(_name, 1);
            Grid.SetColumn(_value, 2);
            Root.Children.Add(bar);
            Root.Children.Add(_icon);
            Root.Children.Add(_name);
            Root.Children.Add(_value);
        }

        public void Set(ImageSource? icon, string name, string value, double fraction)
        {
            _icon.Source = icon;
            _name.Text = name;
            _value.Text = value;
            Anim.To(_scale, ScaleTransform.ScaleXProperty, Math.Clamp(fraction, 0, 1), 400);
        }
    }

    // ---------- tiles ----------

    private sealed class Tile
    {
        private static readonly Brush Normal = new SolidColorBrush(Color.FromRgb(0xE8, 0xC8, 0x78));
        private static readonly Brush Hot = new SolidColorBrush(Color.FromRgb(0xE5, 0x53, 0x4B));

        public Border Root { get; }
        public TextBlock Label { get; }
        private readonly TextBlock _value, _sub;
        private readonly Grid _barTrack;
        private readonly Border _bar;
        private readonly ScaleTransform _scale = new(0, 1);

        public Tile(string label, Style card)
        {
            var muted = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E));
            Label = new TextBlock { Text = label, Foreground = muted, FontSize = 11, FontWeight = FontWeights.SemiBold };
            _sub = new TextBlock { Foreground = muted, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            _value = new TextBlock { Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold };
            _bar = new Border { CornerRadius = new CornerRadius(2), Background = Normal, RenderTransform = _scale };
            _barTrack = new Grid { Height = 4, Margin = new Thickness(0, 4, 0, 0) };
            _barTrack.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)) });
            _barTrack.Children.Add(_bar);

            var header = new Grid();
            header.Children.Add(Label);
            header.Children.Add(_sub);
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(header);
            stack.Children.Add(_value);
            stack.Children.Add(_barTrack);
            Root = new Border { Style = card, Padding = new Thickness(10, 6, 10, 8), Margin = new Thickness(3), Child = stack, Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)) };
        }

        // invert: low is bad (battery), otherwise high is bad.
        public void Set(double? fraction, string value, string sub, bool invert = false)
        {
            _value.Text = value;
            _sub.Text = sub;
            _barTrack.Visibility = fraction is null ? Visibility.Hidden : Visibility.Visible;
            if (fraction is not double f) return;
            Anim.To(_scale, ScaleTransform.ScaleXProperty, Math.Clamp(f, 0, 1), 500);
            _bar.Background = (invert ? f < 0.15 : f > 0.85) ? Hot : Normal;
        }
    }
}
