using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;

namespace Island;

public partial class App : Application
{
    private Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        _single = new Mutex(true, "Island.SingleInstance", out bool first);
        if (!first) { Shutdown(); return; }

        // WPF caps animations at 60 fps by default; match the monitor (e.g. 144 Hz) instead.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = Native.GetRefreshRate() });

        base.OnStartup(e);
    }
}
