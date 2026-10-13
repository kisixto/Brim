using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;

namespace Brim;

public partial class App : Application
{
    private Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        _single = new Mutex(true, "Brim.SingleInstance", out bool first);
        if (!first) { Shutdown(); return; }

        // WPF caps animations at 60 fps by default; match the monitor (e.g. 144 Hz) instead.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = Native.GetRefreshRate() });

        Loc.Apply(Settings.Current.Language);
        // Only an installed or portable copy moves the startup entry to itself; a development build
        // run from the source folder must not take over "start with Windows".
        if (Updater.Supported) AutoStart.RefreshPath();
        base.OnStartup(e);
    }
}
