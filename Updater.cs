using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Brim;

// Background updates from GitHub Releases (Velopack). Checks shortly after launch and every few hours
// while "check for updates automatically" is on; a downloaded update is applied on the next launch,
// or right away from Options → Restart to update.
internal static class Updater
{
    private const string Repo = "https://github.com/kisixto/Brim";

    private static readonly DispatcherTimer Periodic = new() { Interval = TimeSpan.FromHours(6) };
    private static UpdateManager? _manager;
    private static UpdateInfo? _ready;
    private static bool _busy;

    // Status as a Loc key plus argument, so Options can show it in the current language.
    public static string StatusKey { get; private set; } = "UpdIdle";
    public static string StatusArg { get; private set; } = "";
    public static bool ReadyToApply => _ready != null;
    public static event Action? Changed;

    private static UpdateManager? Manager
    {
        get
        {
            if (_manager != null) return _manager;
            try
            {
#if DEBUG
                // Debug builds only: point at a local folder of test packages instead of GitHub.
                var local = Environment.GetEnvironmentVariable("BRIM_UPDATE_SOURCE");
                _manager = string.IsNullOrEmpty(local)
                    ? new UpdateManager(new GithubSource(Repo, null, false))
                    : new UpdateManager(local);
#else
                _manager = new UpdateManager(new GithubSource(Repo, null, false));
#endif
            }
            catch { }
            return _manager;
        }
    }

    // False when running a plain build (e.g. from the source folder) rather than an installed/portable package.
    public static bool Supported => Manager?.IsInstalled == true;

    public static void Start()
    {
        if (!Supported) { Set("UpdDev"); return; }

        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) }; // keep startup snappy
        first.Tick += async (_, _) =>
        {
            first.Stop();
            if (Settings.Current.AutoUpdate) await CheckAsync();
        };
        first.Start();

        Periodic.Tick += async (_, _) => { if (Settings.Current.AutoUpdate) await CheckAsync(); };
        Periodic.Start();
    }

    public static async Task CheckAsync()
    {
        var manager = Manager;
        if (manager == null || !manager.IsInstalled) { Set("UpdDev"); return; }
        if (_busy || _ready != null) return;

        _busy = true;
        try
        {
            Set("UpdChecking");
            var info = await manager.CheckForUpdatesAsync();
            if (info == null)
            {
                Set("UpdLatest", manager.CurrentVersion?.ToString() ?? "");
                return;
            }
            var version = info.TargetFullRelease.Version.ToString();
            Set("UpdDownloading", version);
            await manager.DownloadUpdatesAsync(info, null, CancellationToken.None);
            _ready = info;
            Set("UpdReady", version);
        }
        catch
        {
            Set("UpdFailed");
        }
        finally
        {
            _busy = false;
        }
    }

    // Closes Brim, applies the downloaded update and starts the new version.
    public static void ApplyAndRestart()
    {
        if (_ready != null) Manager?.ApplyUpdatesAndRestart(_ready.TargetFullRelease, null);
    }

    private static void Set(string key, string arg = "")
    {
        StatusKey = key;
        StatusArg = arg;
        Changed?.Invoke();
    }
}
