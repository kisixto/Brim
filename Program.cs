using System;
using Velopack;

namespace Brim;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Must run before anything else: handles Velopack's install/update/uninstall hooks (and exits
        // for those), and applies an update that was downloaded during the previous session.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(true)
            .OnBeforeUninstallFastCallback(_ => AutoStart.SetEnabled(false)) // don't leave a dead startup entry
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
