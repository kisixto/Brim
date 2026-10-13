using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Notchy.Tabs;

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
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = Loc.Culture;
        var time = now.ToString(Settings.Current.Clock24h ? "HH:mm:ss" : Loc.IsKorean ? "tt h:mm:ss" : "h:mm:ss tt", culture);
        if (time == _shownTime) return;
        _shownTime = time;
        TimeText.Text = time;
        TimeText.FontSize = Settings.Current.Clock24h ? 34 : 26; // 12-hour adds 오전/AM
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
