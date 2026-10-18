using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Brim;

public partial class App : Application
{
    private Mutex? _single;
    private bool _sessionEnding;

    protected override void OnStartup(StartupEventArgs e)
    {
        _single = new Mutex(true, "Brim.SingleInstance", out bool first);
        if (!first) { Shutdown(); return; }

        // An error in one feature (a device going away, the clipboard locked, Windows shutting down
        // services at sign-out) must not take the whole app down with an "application error" box.
        // It is written to %AppData%\Brim\crash.log and Brim keeps running.
        DispatcherUnhandledException += (_, args) => { Log(args.Exception); args.Handled = true; };
        TaskScheduler.UnobservedTaskException += (_, args) => { Log(args.Exception); args.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);

        // WPF timer ticks can fire while Windows is signing out and tearing down audio, WMI and the
        // clipboard; quit right away (saving a recording first) instead of keeping the shutdown screen waiting.
        SessionEnding += (_, _) =>
        {
            _sessionEnding = true;
            if (MainWindow is MainWindow main) main.Quit(); else Shutdown();
        };

        // WPF caps animations at 60 fps by default; match the monitor (e.g. 144 Hz) instead.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = Native.GetRefreshRate() });

        Loc.Apply(Settings.Current.Language);
        // Only an installed or portable copy moves the startup entry to itself; a development build
        // run from the source folder must not take over "start with Windows".
        if (Updater.Supported) AutoStart.RefreshPath();
        base.OnStartup(e);
    }

    private void Log(Exception? error)
    {
        if (error == null) return;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Brim");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "crash.log");
            if (File.Exists(file) && new FileInfo(file).Length > 512 * 1024) File.Delete(file);
            var version = typeof(App).Assembly.GetName().Version;
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Brim {version}{(_sessionEnding ? " (signing out)" : "")}\n{error}\n\n");
        }
        catch { }
    }
}
