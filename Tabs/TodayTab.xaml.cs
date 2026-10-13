using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Island.Tabs;

public partial class TodayTab : UserControl
{
    private readonly ObservableCollection<TodoItem> _items = new(Settings.Current.Todos);

    public TodayTab()
    {
        InitializeComponent();
        TodoList.ItemsSource = _items;
        UpdateCount();
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) UpdateDate(); };
    }

    private void UpdateDate()
    {
        var now = DateTime.Now;
        MonthText.Text = now.ToString("MMMM");
        DayText.Text = now.Day.ToString();
        WeekdayText.Text = now.ToString("dddd");
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
