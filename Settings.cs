using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Island;

public sealed class TodoItem : INotifyPropertyChanged
{
    private bool _done;

    public string Text { get; set; } = "";

    public bool Done
    {
        get => _done;
        set { _done = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Done))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

// Saved to %AppData%\Island\settings.json. Clipboard history is never saved.
public sealed class Settings
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Island", "settings.json");

    public List<TodoItem> Todos { get; set; } = new();
    public List<string> Apps { get; set; } = new();
    public string? WallpaperFolder { get; set; }
    public string? LastTab { get; set; }

    [JsonIgnore] public static Settings Current { get; } = Load();

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
