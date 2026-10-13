using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Island.Tabs;

public partial class PowerTab : UserControl
{
    private const double ConfirmSeconds = 3;
    private static readonly Color Idle = Color.FromRgb(0x1C, 0x1C, 0x1E);
    private static readonly Color Armed = Color.FromRgb(0xB3, 0x26, 0x26);

    private sealed record Danger(Button Button, TextBlock Icon, TextBlock Label, Border Bar, string Text);

    private readonly DispatcherTimer _confirmTimer = new() { Interval = TimeSpan.FromSeconds(ConfirmSeconds) };
    private readonly Danger _restart, _shutdown;
    private Danger? _armed;

    public PowerTab()
    {
        InitializeComponent();
        _restart = new Danger(RestartButton, RestartIcon, RestartLabel, RestartBar, "Restart");
        _shutdown = new Danger(ShutdownButton, ShutdownIcon, ShutdownLabel, ShutdownBar, "Shut down");
        _confirmTimer.Tick += (_, _) => Disarm();
    }

    private void Lock_Click(object sender, RoutedEventArgs e) => Native.LockWorkStation();

    private void Sleep_Click(object sender, RoutedEventArgs e) => Native.SetSuspendState(false, false, false);

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (Confirm(_restart)) RunShutdown("/r /t 0");
    }

    private void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        if (Confirm(_shutdown)) RunShutdown("/s /t 0");
    }

    // Restart and shut down need a second click within 3 seconds.
    private bool Confirm(Danger d)
    {
        if (_armed == d) return true;
        Disarm();
        _armed = d;

        Anim.Brush(d.Button, BackgroundProperty, Armed, 260);
        Anim.SwapText(d.Label, "한 번 더");

        // A little shake of the icon to say "are you sure?".
        var rotate = (RotateTransform)d.Icon.RenderTransform;
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new EasingDoubleKeyFrame(-14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))),
                new EasingDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))),
                new EasingDoubleKeyFrame(-6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))),
                new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))),
            },
        });

        var scale = (ScaleTransform)d.Bar.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0, Anim.Ms(ConfirmSeconds * 1000)));
        Anim.To(d.Bar, OpacityProperty, 0.85, 200);

        _confirmTimer.Stop();
        _confirmTimer.Start();
        return false;
    }

    public void Disarm()
    {
        _confirmTimer.Stop();
        if (_armed is not { } d) return;
        _armed = null;

        Anim.Brush(d.Button, BackgroundProperty, Idle, 350);
        Anim.SwapText(d.Label, d.Text);
        Anim.To(d.Bar, OpacityProperty, 0, 220);
    }

    private static void RunShutdown(string args) =>
        Process.Start(new ProcessStartInfo("shutdown", args) { CreateNoWindow = true, UseShellExecute = false });
}
