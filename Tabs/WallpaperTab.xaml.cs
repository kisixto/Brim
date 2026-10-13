using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Island.Tabs;

public partial class WallpaperTab : UserControl
{
    private const int MaxImages = 60;
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
    // Frozen so per-thumbnail color animations get their own copy instead of animating this shared brush.
    private static readonly SolidColorBrush Selected = (SolidColorBrush)new SolidColorBrush(Color.FromRgb(0xE8, 0xC8, 0x78)).GetAsFrozen();

    private bool _loaded;
    private int _loadVersion;

    public WallpaperTab()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue && !_loaded) { _loaded = true; _ = LoadAsync(); }
        };
    }

    private static string CurrentFolder()
    {
        if (Settings.Current.WallpaperFolder is { } saved && Directory.Exists(saved)) return saved;
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return FindImages(pictures).Any() ? pictures : @"C:\Windows\Web\Wallpaper";
    }

    private static System.Collections.Generic.IEnumerable<string> FindImages(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(folder, "*", options)
                        .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
    }

    private async Task LoadAsync()
    {
        int version = ++_loadVersion;
        Thumbs.Children.Clear();
        Scroller.ScrollToHorizontalOffset(0);

        var (folder, files) = await Task.Run(() =>
        {
            var f = CurrentFolder();
            return (f, FindImages(f).Take(MaxImages).ToList());
        });
        FolderText.Text = folder;
        if (version != _loadVersion) return;
        Empty.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var current = Native.GetWallpaper();

        foreach (var file in files)
        {
            var image = await Task.Run(() => LoadThumbnail(file));
            if (version != _loadVersion) return;
            if (image == null) continue;

            var thumb = new Button
            {
                Style = (Style)FindResource("ThumbButton"), Tag = file, ToolTip = Path.GetFileName(file),
                BorderBrush = string.Equals(file, current, StringComparison.OrdinalIgnoreCase) ? Selected : Brushes.Transparent,
                Content = new Border
                {
                    Width = 150, Height = 92, CornerRadius = new CornerRadius(9),
                    Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill },
                },
            };
            thumb.Click += async (_, _) => await ApplyAsync(thumb);
            thumb.Opacity = 0;
            Thumbs.Children.Add(thumb);
            Anim.To(thumb, OpacityProperty, 1, 300);
        }
    }

    private static ImageSource? LoadThumbnail(string file)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 300;
            bitmap.UriSource = new Uri(file);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private async Task ApplyAsync(Button thumb)
    {
        foreach (var child in Thumbs.Children.OfType<Button>())
            Anim.Brush(child, BorderBrushProperty, child == thumb ? Selected.Color : Colors.Transparent, 250);
        var file = (string)thumb.Tag;
        await Task.Run(() => Native.SetWallpaper(file));
    }

    private async void Random_Click(object sender, RoutedEventArgs e)
    {
        var thumbs = Thumbs.Children.OfType<Button>().ToList();
        if (thumbs.Count == 0) return;
        var pick = thumbs[Random.Shared.Next(thumbs.Count)];
        pick.BringIntoView();
        await ApplyAsync(pick);
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "배경화면 사진이 있는 폴더", InitialDirectory = FolderText.Text };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        Settings.Current.WallpaperFolder = dialog.FolderName;
        Settings.Current.Save();
        _ = LoadAsync();
    }

    // Vertical wheel scrolls the strip sideways.
    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
}
