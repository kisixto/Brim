using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brim.Tabs;

public partial class TodayTab : UserControl
{
    private readonly ObservableCollection<TodoItem> _items = new(Settings.Current.Todos);
    // Polled faster than once a second so the seconds flip close to the real boundary.
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private string _shownTime = "";

    public TodayTab()
    {
        InitializeComponent();
        TodoList.ItemsSource = _items;
        UpdateCount();
        _clock.Tick += (_, _) => UpdateClock();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { UpdateClock(); _clock.Start(); }
            else _clock.Stop();
        };

        // The countdown keeps running while the island is closed or another tab is open.
        _timerTick.Tick += (_, _) => TimerTick();
        ShowTimer(TimeSpan.FromMinutes(Settings.Current.TimerMinutes));
    }

    // ---------- timer ----------

    private enum TimerState { Idle, Running, Paused, Done }

    private static readonly Color Alarm = Color.FromRgb(0xE5, 0x53, 0x4B);
    private readonly DispatcherTimer _timerTick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private TimerState _state;
    private DateTime _endsAt;
    private TimeSpan _left;
    private int _beeps;

    private int Minutes
    {
        get => Settings.Current.TimerMinutes;
        set
        {
            Settings.Current.TimerMinutes = Math.Clamp(value, 1, 180);
            Settings.Current.Save();
            ShowTimer(TimeSpan.FromMinutes(Settings.Current.TimerMinutes));
        }
    }

    // Length can only be changed while the timer isn't counting.
    private void TimerText_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_state != TimerState.Idle) return;
        Minutes += e.Delta > 0 ? 1 : -1;
        e.Handled = true;
    }

    private void TimerMinus_Click(object sender, RoutedEventArgs e) { if (_state == TimerState.Idle) Minutes--; }
    private void TimerPlus_Click(object sender, RoutedEventArgs e) { if (_state == TimerState.Idle) Minutes++; }

    private void TimerPlay_Click(object sender, RoutedEventArgs e)
    {
        switch (_state)
        {
            case TimerState.Idle:
                _endsAt = DateTime.Now.AddMinutes(Minutes);
                SetState(TimerState.Running);
                break;
            case TimerState.Running:
                _left = _endsAt - DateTime.Now;
                SetState(TimerState.Paused);
                break;
            case TimerState.Paused:
                _endsAt = DateTime.Now + _left;
                SetState(TimerState.Running);
                break;
            case TimerState.Done:
                ResetTimer();
                break;
        }
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e) => ResetTimer();

    private void ResetTimer()
    {
        SetState(TimerState.Idle);
        ShowTimer(TimeSpan.FromMinutes(Minutes));
    }

    private void SetState(TimerState state)
    {
        _state = state;
        if (state is TimerState.Running or TimerState.Done) _timerTick.Start();
        else _timerTick.Stop();

        TimerPlay.Content = state == TimerState.Running ? "" : ""; // pause : play
        TimerMinus.IsEnabled = TimerPlus.IsEnabled = state == TimerState.Idle;
        TimerMinus.Opacity = TimerPlus.Opacity = state == TimerState.Idle ? 1 : 0.35;

        bool done = state == TimerState.Done;
        Anim.Brush(TimerText, TextBlock.ForegroundProperty, done ? Alarm : Colors.White, 250);
        if (done) TimerTitle.SetResourceReference(TextBlock.TextProperty, "L.TimerDone");
        else TimerTitle.SetResourceReference(TextBlock.TextProperty, "L.Timer");
        if (!done) TimerText.BeginAnimation(OpacityProperty, null);
    }

    private void TimerTick()
    {
        if (_state == TimerState.Done)
        {
            // Three short chimes, one a second, then stay red until dismissed.
            if (_beeps > 0 && DateTime.Now >= _endsAt.AddSeconds(3 - _beeps))
            {
                _beeps--;
                System.Media.SystemSounds.Exclamation.Play();
            }
            return;
        }

        var left = _endsAt - DateTime.Now;
        if (left > TimeSpan.Zero) { ShowTimer(left); return; }

        ShowTimer(TimeSpan.Zero);
        _beeps = 3;
        SetState(TimerState.Done);
        TimerText.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, 0.35, Anim.Ms(600))
        {
            AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
        });
        (Window.GetWindow(this) as MainWindow)?.ShowTab(this); // open the island so the alert is seen
    }

    private void ShowTimer(TimeSpan left)
    {
        // Round up so it reads 05:00 right after starting and 00:01 in its last second.
        var shown = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        TimerText.Text = shown.TotalHours >= 1 ? shown.ToString(@"h\:mm\:ss") : shown.ToString(@"mm\:ss");
        TimerText.FontSize = shown.TotalHours >= 1 ? 24 : 28;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = Loc.Culture;
        var time = now.ToString(Settings.Current.Clock24h ? "HH:mm:ss" : Loc.IsKorean ? "tt h:mm:ss" : "h:mm:ss tt", culture);
        if (time == _shownTime) return;
        _shownTime = time;
        TimeText.Text = time;
        TimeText.FontSize = Settings.Current.Clock24h ? 30 : 22; // 12-hour adds 오전/AM
        DateText.Text = now.ToString(Loc.IsKorean ? "M월 d일 dddd" : "dddd, MMMM d", culture);
    }

    private void NewTodo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var text = NewTodo.Text.Trim();
        if (text.Length == 0) return;
        _items.Add(new TodoItem { Text = text });
        NewTodo.Clear();
        Save();
    }

    private void NewTodo_TextChanged(object sender, TextChangedEventArgs e) =>
        Hint.Visibility = NewTodo.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Check_Click(object sender, RoutedEventArgs e) => Save();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TodoItem item)
        {
            _items.Remove(item);
            Save();
        }
    }

    private void Save()
    {
        Settings.Current.Todos = _items.ToList();
        Settings.Current.Save();
        UpdateCount();
    }

    private void UpdateCount() => CountText.Text = $"{_items.Count(i => i.Done)}/{_items.Count}";
}
