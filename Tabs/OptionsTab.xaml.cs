using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Notchy.Tabs;

public partial class OptionsTab : UserControl
{
    public const string RepoUrl = "https://github.com/kisixto/Notchy";

    // Animated stand-in for the scroll position, so the wheel glides instead of jumping.
    private static readonly DependencyProperty ScrollOffsetProperty = DependencyProperty.Register(
        "ScrollOffset", typeof(double), typeof(OptionsTab),
        new PropertyMetadata(0.0, (d, e) => ((OptionsTab)d).Scroller.ScrollToVerticalOffset((double)e.NewValue)));

    private static Settings S => Settings.Current;
    private MainWindow? Host => Window.GetWindow(this) as MainWindow;
    private double _scrollTarget;
    private bool _scrolling;

    public OptionsTab()
    {
        InitializeComponent();
        Loaded += (_, _) => { if (Rows.Children.Count == 0) Build(); };
    }

    private void Build()
    {
        var general = Section("OptGeneral");
        Toggle(general, "OptStartup", () => AutoStart.IsEnabled, AutoStart.SetEnabled);
        Choice(general, "OptLanguage", () => S.Language, v => { S.Language = v; Loc.Apply(v); },
            ("auto", "OptLangAuto", null), ("ko", null, "한국어"), ("en", null, "English"));

        var behavior = Section("OptBehavior");
        Choice(behavior, "OptOpenSpeed", () => S.OpenSpeed, v => { S.OpenSpeed = v; Host?.ApplyOpenSpeed(); },
            ("fast", "OptFast", null), ("normal", "OptNormal", null), ("slow", "OptSlow", null));
        Toggle(behavior, "OptReduceMotion", () => S.ReduceMotion, v => S.ReduceMotion = v);

        var clock = Section("OptClock");
        Toggle(clock, "Opt24h", () => S.Clock24h, v => S.Clock24h = v);

        var capture = Section("OptCapture");
        Toggle(capture, "OptRecordAudio", () => S.RecordAudio, v => S.RecordAudio = v);
        Toggle(capture, "OptHideOwn", () => S.HideFromOwnCaptures, v => S.HideFromOwnCaptures = v);

        var tabs = Section("OptTabs");
        foreach (var name in Host?.HideableTabs ?? Array.Empty<string>())
        {
            Toggle(tabs, null, () => !S.HiddenTabs.Contains(name), shown =>
            {
                S.HiddenTabs.Remove(name);
                if (!shown) S.HiddenTabs.Add(name);
                Host?.ApplyTabVisibility();
            }, literal: name);
        }

        var about = Section("OptAbout");
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        void UpdateVersion() => versionText.Text = Loc.T("OptVersion", $"{version?.Major}.{version?.Minor}.{version?.Build}");
        UpdateVersion();
        Loc.Changed += UpdateVersion;
        Row(about, null, versionText, literal: "Notchy");

        var links = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        links.Children.Add(Link("OptGitHub", () => Open(RepoUrl)));
        links.Children.Add(Link("OptSettingsFolder", () => Open(Settings.Folder)));
        links.Children.Add(Link("Quit", () => Host?.Quit()));
        about.Children.Add(links);
    }

    // ---------- row builders ----------

    private StackPanel Section(string titleKey)
    {
        var title = new TextBlock
        {
            Foreground = (Brush)FindResource("Muted"), FontSize = 11, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(4, Rows.Children.Count == 0 ? 0 : 12, 0, 6),
        };
        title.SetResourceReference(TextBlock.TextProperty, "L." + titleKey);
        var body = new StackPanel();
        Rows.Children.Add(title);
        Rows.Children.Add(new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(12, 2, 12, 2), Child = body });
        return body;
    }

    private static void Row(Panel section, string? labelKey, UIElement control, string? literal = null)
    {
        var label = new TextBlock { Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        if (literal != null) label.Text = literal;
        else label.SetResourceReference(TextBlock.TextProperty, "L." + labelKey);

        var row = new Grid { MinHeight = 36 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(control, 1);
        row.Children.Add(label);
        row.Children.Add(control);
        section.Children.Add(row);
    }

    private void Toggle(Panel section, string? labelKey, Func<bool> get, Action<bool> set, string? literal = null)
    {
        var toggle = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = get(), VerticalAlignment = VerticalAlignment.Center };
        toggle.Click += (_, _) =>
        {
            set(toggle.IsChecked == true);
            Settings.Current.Save();
        };
        Row(section, labelKey, toggle, literal);
    }

    // options: (value, label key, or literal label when the key is null)
    private void Choice(Panel section, string labelKey, Func<string> get, Action<string> set,
                        params (string Value, string? Key, string? Literal)[] options)
    {
        var group = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x29)), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(2), Child = group,
        };
        string groupName = "opt-" + labelKey;
        foreach (var (value, key, literal) in options)
        {
            var choice = new RadioButton
            {
                Style = (Style)FindResource("Segment"), GroupName = groupName, IsChecked = get() == value,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E)),
            };
            if (key != null) choice.SetResourceReference(ContentProperty, "L." + key);
            else choice.Content = literal;
            choice.Checked += (_, _) =>
            {
                if (get() == value) return;
                set(value);
                Settings.Current.Save();
            };
            group.Children.Add(choice);
        }
        Row(section, labelKey, border);
    }

    private Button Link(string key, Action onClick)
    {
        var link = new Button { Style = (Style)FindResource("TextButton"), Margin = new Thickness(0, 0, 12, 0) };
        link.SetResourceReference(ContentProperty, "L." + key);
        link.Click += (_, _) => onClick();
        return link;
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }

    // ---------- smooth wheel scrolling ----------

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Scroller.ScrollableHeight <= 0) return;
        if (!_scrolling)
        {
            // Start from wherever the list is now (the scrollbar may have been dragged).
            _scrollTarget = Scroller.VerticalOffset;
            BeginAnimation(ScrollOffsetProperty, null);
            SetValue(ScrollOffsetProperty, Scroller.VerticalOffset);
        }
        _scrollTarget = Math.Clamp(_scrollTarget - e.Delta * 0.5, 0, Scroller.ScrollableHeight);
        _scrolling = true;
        Anim.To(this, ScrollOffsetProperty, _scrollTarget, 280, Anim.OutQuart, completed: (_, _) => _scrolling = false);
        e.Handled = true;
    }
}
