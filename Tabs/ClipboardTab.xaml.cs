using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Brim.Tabs;

// One history entry: either text, or an image kept as compressed PNG plus a small thumbnail.
// Key identifies duplicates (the text itself, or a hash of the image).
public sealed record ClipItem(string Key, string Preview, string Time, string? Text, byte[]? Png, ImageSource? Thumb);

// In-memory only: history is gone when Brim quits.
public partial class ClipboardTab : UserControl
{
    private const int MaxItems = 30;
    private const int MaxImages = 10; // images can be several MB each, so keep fewer of them
    private readonly ObservableCollection<ClipItem> _items = new();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1.2) };
    private DateTime _ownWriteAt; // when Brim itself last put something on the clipboard

    public ClipboardTab()
    {
        InitializeComponent();
        List.ItemsSource = _items;
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); Status.SetResourceReference(TextBlock.TextProperty, "L.ClipHint"); };
    }

    public async void Capture()
    {
        // Re-copying an item from the list fires a clipboard update too; that item was already moved to the top.
        if (DateTime.Now - _ownWriteAt < TimeSpan.FromMilliseconds(800)) return;

        string? text = null;
        BitmapSource? image = null;
        for (int attempt = 0; attempt < 3 && text == null && image == null; attempt++)
        {
            try
            {
                // Password managers set this flag to keep secrets out of clipboard history.
                if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;
                // Prefer text when both are offered (e.g. a spreadsheet cell also comes as a picture).
                if (Clipboard.ContainsText()) text = Clipboard.GetText();
                else if (Clipboard.ContainsImage()) image = Clipboard.GetImage();
                else return;
            }
            catch (COMException) { Thread.Sleep(30); } // clipboard is locked by another app
        }

        if (image != null)
        {
            // The clipboard image belongs to the UI thread; freeze a copy so the encoding thread can read it.
            BitmapSource frozen;
            try
            {
                frozen = new WriteableBitmap(image);
                frozen.Freeze();
            }
            catch { return; }
            var item = await Task.Run(() => MakeImageItem(frozen));
            if (item != null) Add(item);
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            var preview = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (preview.Length > 200) preview = preview[..200];
            Add(new ClipItem(text, preview, Now(), text, null, null));
        }
    }

    private static string Now() => DateTime.Now.ToString("HH:mm");

    // Off the UI thread: encode to PNG (much smaller than raw pixels) and make a ~48px-tall thumbnail.
    private static ClipItem? MakeImageItem(BitmapSource frozen)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frozen));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            var png = stream.ToArray();

            double scale = Math.Min(1.0, 96.0 / frozen.PixelHeight); // 2x of the 48px row height for sharpness
            var thumb = new TransformedBitmap(frozen, new ScaleTransform(scale, scale));
            thumb.Freeze();

            var key = Convert.ToHexString(SHA1.HashData(png));
            var preview = Loc.T("ClipImage", frozen.PixelWidth, frozen.PixelHeight);
            return new ClipItem(key, preview, Now(), null, png, thumb);
        }
        catch { return null; }
    }

    private void Add(ClipItem item)
    {
        var existing = _items.FirstOrDefault(i => i.Key == item.Key);
        if (existing != null) _items.Remove(existing);
        _items.Insert(0, item);

        foreach (var oldImage in _items.Where(i => i.Png != null).Skip(MaxImages).ToList())
            _items.Remove(oldImage);
        while (_items.Count > MaxItems) _items.RemoveAt(_items.Count - 1);
        Empty.Visibility = Visibility.Collapsed;
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ClipItem item) return;
        try
        {
            _ownWriteAt = DateTime.Now;
            if (item.Png != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new MemoryStream(item.Png);
                bitmap.EndInit();
                Clipboard.SetImage(bitmap);
            }
            else
            {
                Clipboard.SetText(item.Text!);
            }
        }
        catch (COMException) { return; }

        // Most recently copied goes to the top, just like copying it fresh.
        _items.Remove(item);
        _items.Insert(0, item with { Time = Now() });

        Status.Text = Loc.T("ClipCopied");
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        Empty.Visibility = Visibility.Visible;
    }
}
