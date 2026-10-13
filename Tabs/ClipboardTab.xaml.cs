using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Island.Tabs;

public sealed record ClipItem(string Text, string Preview, string Time);

// In-memory only: history is gone when the island quits.
public partial class ClipboardTab : UserControl
{
    private const int MaxItems = 30;
    private readonly ObservableCollection<ClipItem> _items = new();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1.2) };

    public ClipboardTab()
    {
        InitializeComponent();
        List.ItemsSource = _items;
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); Status.Text = "클릭하면 다시 복사돼요"; };
    }

    public void Capture()
    {
        string? text = null;
        for (int attempt = 0; attempt < 3 && text == null; attempt++)
        {
            try
            {
                // Password managers set this flag to keep secrets out of clipboard history.
                if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;
                if (!Clipboard.ContainsText()) return;
                text = Clipboard.GetText();
            }
            catch (COMException) { Thread.Sleep(30); } // clipboard is locked by another app
        }
        if (string.IsNullOrWhiteSpace(text)) return;

        var existing = _items.FirstOrDefault(i => i.Text == text);
        if (existing != null) _items.Remove(existing);

        var preview = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (preview.Length > 200) preview = preview[..200];
        _items.Insert(0, new ClipItem(text, preview, DateTime.Now.ToString("HH:mm")));
        while (_items.Count > MaxItems) _items.RemoveAt(_items.Count - 1);
        Empty.Visibility = Visibility.Collapsed;
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ClipItem item) return;
        try { Clipboard.SetText(item.Text); } catch (COMException) { return; }
        Status.Text = "복사됨 ✓";
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        Empty.Visibility = Visibility.Visible;
    }
}
