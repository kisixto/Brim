using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Brim.Tabs;

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

        _scheduleTimer.Tick += (_, _) => TickSchedule();
        Loc.Changed += UpdateScheduleUi;
        Loaded += (_, _) => { if (!_restored) { _restored = true; RestoreSchedule(); } };
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
        Anim.SwapText(d.Label, Loc.T("PowerAgain"));

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

    // ---------- scheduled shutdown / restart / sleep ----------
    //
    // Brim keeps the time itself instead of using `shutdown /t`, because a delayed `shutdown` forces apps
    // closed (/f is implied) and unsaved work would be lost. At the scheduled moment it runs the normal,
    // non-forced action. The schedule is saved, so it survives Brim restarting (update, crash).

    // If the moment passed while the PC slept or Brim wasn't running, don't act on it after the fact.
    private static readonly TimeSpan MissedAfter = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WarnBefore = TimeSpan.FromMinutes(1);

    private readonly DispatcherTimer _scheduleTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _restored, _warned;
    private string? _notice; // one-off message (e.g. missed schedule) shown in the status line

    private static Settings S => Settings.Current;
    private MainWindow? Host => Window.GetWindow(this) as MainWindow;
    private bool Scheduled => S.ScheduledAt != null && S.ScheduledPower != null;

    private void Timer_Click(object sender, RoutedEventArgs e)
    {
        Disarm();
        PickerHint.Text = "";
        TimeInput.Text = DateTime.Now.AddHours(1).ToString("HH:mm");
        ShowPicker(true);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowPicker(false);

    private void Preset_Click(object sender, RoutedEventArgs e) =>
        Schedule(DateTime.Now.AddMinutes(int.Parse((string)((Button)sender).Tag)));

    private void SetAt_Click(object sender, RoutedEventArgs e) => ScheduleAtInput();

    private void TimeInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) ScheduleAtInput();
    }

    // "23:30" means the next 23:30: today if it's still ahead, otherwise tomorrow.
    private void ScheduleAtInput()
    {
        if (!TimeSpan.TryParseExact(TimeInput.Text.Trim(), new[] { @"h\:mm", @"hh\:mm" }, null, out var time) || time.TotalHours >= 24)
        {
            PickerHint.Text = Loc.T("SchedBadTime");
            return;
        }
        var at = DateTime.Today + time;
        if (at <= DateTime.Now) at = at.AddDays(1);
        Schedule(at);
    }

    private void Schedule(DateTime at)
    {
        S.ScheduledPower = ActRestart.IsChecked == true ? "restart" : ActSleep.IsChecked == true ? "sleep" : "shutdown";
        S.ScheduledAt = at;
        S.Save();
        _warned = false;
        _notice = null;
        _scheduleTimer.Start();
        ShowPicker(false);
        UpdateScheduleUi();
    }

    private void CancelSchedule_Click(object sender, RoutedEventArgs e)
    {
        _notice = null;
        ClearSchedule();
    }

    private void ClearSchedule()
    {
        S.ScheduledPower = null;
        S.ScheduledAt = null;
        S.Save();
        _scheduleTimer.Stop();
        UpdateScheduleUi();
    }

    private void RestoreSchedule()
    {
        if (!Scheduled) return;
        if (DateTime.Now - S.ScheduledAt!.Value > MissedAfter) { Missed(); return; }
        _scheduleTimer.Start();
        UpdateScheduleUi();
    }

    private void TickSchedule()
    {
        if (!Scheduled) { _scheduleTimer.Stop(); return; }
        var remaining = S.ScheduledAt!.Value - DateTime.Now;

        if (remaining < -MissedAfter) { Missed(); return; } // e.g. woke up long after the time
        if (remaining <= TimeSpan.Zero) { Execute(); return; }

        if (remaining <= WarnBefore && !_warned)
        {
            _warned = true;
            Host?.ShowTab(this); // pop the island open so the countdown and Cancel are in view
        }
        UpdateScheduleUi();
    }

    private void Missed()
    {
        ClearSchedule();
        _notice = Loc.T("SchedMissed");
        UpdateScheduleUi();
    }

    private void Execute()
    {
        var action = S.ScheduledPower;
        ClearSchedule();
        // Finish any recording first so the video isn't left corrupt, then act. No /f: apps with unsaved
        // work still get to object, the same as shutting down from the Start menu.
        Host?.BeforeExit(() =>
        {
            switch (action)
            {
                case "restart": RunShutdown("/r /t 0"); break;
                case "sleep": Native.SetSuspendState(false, false, false); break;
                default: RunShutdown("/s /t 0"); break;
            }
        });
    }

    private void ShowPicker(bool show)
    {
        var (from, to) = show ? ((FrameworkElement)Buttons, (FrameworkElement)Picker) : (Picker, Buttons);
        from.Visibility = Visibility.Collapsed;
        to.Visibility = Visibility.Visible;
        to.Opacity = 0;
        Anim.To(to, OpacityProperty, 1, 200);
        UpdateScheduleUi();
    }

    private void UpdateScheduleUi()
    {
        bool pickerOpen = Picker.Visibility == Visibility.Visible;
        Host?.SetScheduled(Scheduled);

        if (Scheduled)
        {
            var remaining = S.ScheduledAt!.Value - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            var clock = remaining.TotalHours >= 1 ? remaining.ToString(@"h\:mm\:ss") : remaining.ToString(@"mm\:ss");
            var action = Loc.T(S.ScheduledPower switch { "restart" => "ActRestart", "sleep" => "ActSleep", _ => "ActShutdown" });
            var at = S.ScheduledAt.Value.ToString(Loc.IsKorean ? "tt h:mm" : "h:mm tt", Loc.Culture);
            StatusText.Text = Loc.T("SchedStatus", clock, action, at);
            CancelButton.Visibility = Visibility.Visible;
        }
        else if (_notice != null)
        {
            StatusText.Text = _notice;
            CancelButton.Visibility = Visibility.Collapsed;
        }
        Status.Visibility = (Scheduled || _notice != null) && !pickerOpen ? Visibility.Visible : Visibility.Collapsed;
    }
}
