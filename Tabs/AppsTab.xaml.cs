using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Brim.Tabs;

// Pinned apps, optionally sorted into user-made groups (Game, Browser, ...). "All" shows everything;
// an app belongs to at most one group. Groups live in Settings.AppGroups / AppGroupOf.
public partial class AppsTab : UserControl
{
    private static Settings S => Settings.Current;
    private string? Selected => S.AppsGroup != null && S.AppGroups.Contains(S.AppsGroup) ? S.AppsGroup : null;

    public AppsTab()
    {
        InitializeComponent();
        Rebuild();
        Loc.Changed += Rebuild;
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!(File.Exists(path) || Directory.Exists(path))) continue;
            if (!S.Apps.Contains(path)) S.Apps.Add(path);
            if (Selected is string group) S.AppGroupOf[path] = group; // dropped while a group is open → into that group
        }
        S.Save();
        Rebuild();
    }

    private void Rebuild()
    {
        BuildGroupBar();
        Tiles.Children.Clear();
        var shown = S.Apps.Where(p => Selected == null || S.AppGroupOf.GetValueOrDefault(p) == Selected).ToList();
        foreach (var path in shown) Tiles.Children.Add(MakeTile(path));
        Tiles.Children.Add(MakeAddTile());

        Hint.Text = S.Apps.Count == 0 ? Loc.T("AppsHint") : shown.Count == 0 ? Loc.T("AppsGroupEmpty") : "";
        Hint.Visibility = Hint.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Select(string? group)
    {
        S.AppsGroup = group;
        S.Save();
        Rebuild();
    }

    // ---------- group bar ----------

    private void BuildGroupBar()
    {
        GroupBar.Children.Clear();
        GroupBar.Children.Add(GroupPill(null));
        foreach (var group in S.AppGroups) GroupBar.Children.Add(GroupPill(group));

        var add = new Button
        {
            Style = (Style)FindResource("IconButton"), Content = "", FontSize = 11, Width = 24, Height = 24,
            Margin = new Thickness(4, 0, 0, 0), ToolTip = Loc.T("AppsNewGroup"),
        };
        add.Click += (_, _) => EditGroupName(null);
        GroupBar.Children.Add(add);
    }

    private RadioButton GroupPill(string? group)
    {
        var pill = new RadioButton
        {
            Style = (Style)FindResource("Segment"), GroupName = "appgroups", IsChecked = Selected == group,
            Content = group ?? Loc.T("AppsAll"), Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E)),
        };
        pill.Checked += (_, _) => { if (Selected != group) Select(group); };
        if (group != null)
        {
            var rename = new MenuItem { Header = Loc.T("AppsRename") };
            rename.Click += (_, _) => EditGroupName(group);
            var delete = new MenuItem { Header = Loc.T("AppsDeleteGroup") };
            delete.Click += (_, _) => DeleteGroup(group);
            pill.ContextMenu = new ContextMenu { Items = { rename, delete } };
        }
        return pill;
    }

    // Inline name editor shown in the group bar: Enter saves, Esc or clicking away cancels.
    // `existing` null = create a new group.
    private void EditGroupName(string? existing)
    {
        var box = new TextBox
        {
            Style = (Style)FindResource("DarkTextBox"), Text = existing ?? "", MinWidth = 70, MaxLength = 20,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var frame = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2D)), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(2, 0, 2, 0), Child = box,
            ToolTip = Loc.T("AppsGroupName"),
        };

        int index = existing == null ? GroupBar.Children.Count - 1 : S.AppGroups.IndexOf(existing) + 1;
        if (existing != null) GroupBar.Children.RemoveAt(index);
        GroupBar.Children.Insert(index, frame);

        bool done = false;
        void Finish(bool save)
        {
            if (done) return;
            done = true;
            var name = box.Text.Trim();
            if (save && name.Length > 0 && name != existing && !S.AppGroups.Contains(name))
            {
                if (existing == null)
                {
                    S.AppGroups.Add(name);
                    S.AppsGroup = name;
                }
                else
                {
                    S.AppGroups[S.AppGroups.IndexOf(existing)] = name;
                    foreach (var path in S.AppGroupOf.Where(kv => kv.Value == existing).Select(kv => kv.Key).ToList())
                        S.AppGroupOf[path] = name;
                    if (S.AppsGroup == existing) S.AppsGroup = name;
                }
                S.Save();
            }
            Rebuild();
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Finish(true);
            else if (e.Key == Key.Escape) Finish(false);
        };
        box.LostKeyboardFocus += (_, _) => Finish(true);
        box.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
    }

    private void DeleteGroup(string group)
    {
        S.AppGroups.Remove(group);
        foreach (var path in S.AppGroupOf.Where(kv => kv.Value == group).Select(kv => kv.Key).ToList())
            S.AppGroupOf.Remove(path);
        if (S.AppsGroup == group) S.AppsGroup = null;
        S.Save();
        Rebuild();
    }

    // ---------- tiles ----------

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

        // Right-click: move to a group, or remove.
        var moveTo = new MenuItem { Header = Loc.T("AppsMoveTo") };
        var current = S.AppGroupOf.GetValueOrDefault(path);
        foreach (var group in S.AppGroups.Prepend(null))
        {
            var target = group;
            var item = new MenuItem { Header = target ?? Loc.T("AppsNoGroup"), IsCheckable = true, IsChecked = current == target };
            item.Click += (_, _) =>
            {
                if (target == null) S.AppGroupOf.Remove(path);
                else S.AppGroupOf[path] = target;
                S.Save();
                Rebuild();
            };
            moveTo.Items.Add(item);
        }
        var remove = new MenuItem { Header = Loc.T("AppsRemove") };
        remove.Click += (_, _) =>
        {
            S.Apps.Remove(path);
            S.AppGroupOf.Remove(path);
            S.Save();
            Rebuild();
        };

        var tile = new Button
        {
            Style = (Style)FindResource("AppTile"), Content = stack, ToolTip = path,
            ContextMenu = new ContextMenu { Items = { moveTo, remove } },
        };
        if (S.AppGroups.Count == 0) moveTo.Visibility = Visibility.Collapsed; // nothing to move to yet
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
