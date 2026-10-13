using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Notchy.Tabs;

public partial class AppsTab : UserControl
{
    public AppsTab()
    {
        InitializeComponent();
        Rebuild();
        Loc.Changed += Rebuild;
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        var apps = Settings.Current.Apps;
        foreach (var path in paths)
            if ((File.Exists(path) || Directory.Exists(path)) && !apps.Contains(path))
                apps.Add(path);
        Settings.Current.Save();
        Rebuild();
    }

    private void Rebuild()
    {
        Tiles.Children.Clear();
        foreach (var path in Settings.Current.Apps)
            Tiles.Children.Add(MakeTile(path));
        Tiles.Children.Add(MakeAddTile());
        Hint.Visibility = Settings.Current.Apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button MakeTile(string path)
    {
        var (icon, name) = Native.GetFileInfo(path);
        var stack = new StackPanel();
        stack.Children.Add(new Image { Source = icon, Width = 32, Height = 32 });
        stack.Children.Add(new TextBlock
        {
            Text = name, FontSize = 10, Width = 58, Margin = new Thickness(0, 5, 0, 0),
            TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var remove = new MenuItem { Header = Loc.T("AppsRemove") };
        remove.Click += (_, _) =>
        {
            Settings.Current.Apps.Remove(path);
            Settings.Current.Save();
            Rebuild();
        };

        var tile = new Button
        {
            Style = (Style)FindResource("AppTile"), Content = stack, ToolTip = path,
            ContextMenu = new ContextMenu { Items = { remove } },
        };
        tile.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { }
        };
        return tile;
    }

    private Button MakeAddTile()
    {
        var stack = new StackPanel();
        stack.Children.Add(new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x5A)), BorderThickness = new Thickness(1.5),
            Child = new TextBlock
            {
                Text = "", FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        });
        stack.Children.Add(new TextBlock
        {
            Text = Loc.T("AppsAdd"), FontSize = 10, Margin = new Thickness(0, 5, 0, 0), TextAlignment = TextAlignment.Center,
            Foreground = (Brush)FindResource("Muted"),
        });

        var tile = new Button { Style = (Style)FindResource("AppTile"), Content = stack };
        tile.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.T("AppsPickTitle"),
                Filter = Loc.T("AppsFilter"),
                Multiselect = true,
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                AddFiles(dialog.FileNames);
        };
        return tile;
    }
}
