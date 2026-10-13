using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Notchy.Tabs;

public partial class StatsTab : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Tile _cpu, _ram, _disk, _power;
    private long _lastIdle, _lastTotal;

    public StatsTab()
    {
        InitializeComponent();
        _cpu = AddTile("CPU");
        _ram = AddTile("Memory");
        _disk = AddTile("Disk");
        _power = AddTile("Battery");

        _timer.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { SampleCpu(); Refresh(); _timer.Start(); }
            else _timer.Stop();
        };
    }

    private Tile AddTile(string labelKey)
    {
        var tile = new Tile(Loc.T(labelKey), (Style)FindResource("Card"));
        if (labelKey != "CPU") tile.Label.SetResourceReference(TextBlock.TextProperty, "L." + labelKey);
        Tiles.Children.Add(tile.Root);
        return tile;
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
    }

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
            Label = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E)), FontSize = 11, FontWeight = FontWeights.SemiBold };
            _value = new TextBlock { Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0) };
            _sub = new TextBlock { Foreground = Label.Foreground, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
            _bar = new Border { CornerRadius = new CornerRadius(2), Background = Normal, RenderTransform = _scale };
            _barTrack = new Grid { Height = 4, Margin = new Thickness(0, 10, 0, 0) };
            _barTrack.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)) });
            _barTrack.Children.Add(_bar);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(Label);
            stack.Children.Add(_value);
            stack.Children.Add(_sub);
            stack.Children.Add(_barTrack);
            Root = new Border { Style = card, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(4, 0, 4, 0), Child = stack };
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
