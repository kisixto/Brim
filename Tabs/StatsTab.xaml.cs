using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Island.Tabs;

public partial class StatsTab : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Tile _cpu, _ram, _disk, _power;
    private long _lastIdle, _lastTotal;

    public StatsTab()
    {
        InitializeComponent();
        _cpu = AddTile("CPU");
        _ram = AddTile("메모리");
        _disk = AddTile("디스크");
        _power = AddTile("배터리");

        _timer.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { SampleCpu(); Refresh(); _timer.Start(); }
            else _timer.Stop();
        };
    }

    private Tile AddTile(string label)
    {
        var tile = new Tile(label, (Style)FindResource("Card"));
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
        _cpu.Set(cpu, $"{cpu * 100:0}%", $"{Environment.ProcessorCount}스레드");

        var mem = new Native.MemoryStatus { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MemoryStatus>() };
        Native.GlobalMemoryStatusEx(ref mem);
        double usedGb = (mem.TotalPhys - mem.AvailPhys) / 1073741824.0, totalGb = mem.TotalPhys / 1073741824.0;
        _ram.Set(mem.MemoryLoad / 100.0, $"{mem.MemoryLoad}%", $"{usedGb:0.0} / {totalGb:0} GB");

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            double used = 1 - (double)drive.AvailableFreeSpace / drive.TotalSize;
            _disk.Set(used, $"{used * 100:0}%", $"{drive.Name.TrimEnd('\\')} 여유 {drive.AvailableFreeSpace / 1073741824.0:0} GB");
        }
        catch { }

        Native.GetSystemPowerStatus(out var power);
        if ((power.BatteryFlag & 128) == 0 && power.BatteryLifePercent <= 100)
        {
            _power.Label.Text = "배터리";
            _power.Set(power.BatteryLifePercent / 100.0, $"{power.BatteryLifePercent}%",
                       power.ACLineStatus == 1 ? "충전 중" : "배터리 사용 중", invert: true);
        }
        else
        {
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            _power.Label.Text = "가동 시간";
            _power.Set(null, up.TotalDays >= 1 ? $"{(int)up.TotalDays}일" : $"{up.Hours}시간",
                       up.TotalDays >= 1 ? $"{up.Hours}시간 {up.Minutes}분" : $"{up.Minutes}분");
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
