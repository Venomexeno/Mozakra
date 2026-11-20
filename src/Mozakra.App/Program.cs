using Microsoft.Extensions.DependencyInjection;
using Mozakra.App.Composition;
using Mozakra.App.Services;
using Velopack;

namespace Mozakra.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack must run first: it handles install/update/uninstall hooks and may exit the process.
        // Setup turns on "Start with Windows"; uninstall removes the Run value (Plan 07).
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => new StartupService().Set(true))
            .OnBeforeUninstallFastCallback(_ => new StartupService().Set(false))
            .Run();

        // One instance only. A second launch just asks the running one to show the note.
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceService.MutexName, out bool isFirst);
        if (!isFirst)
        {
            SingleInstanceService.SignalRunningInstance();
            return 0;
        }

        using var services = new ServiceCollection()
            .AddMozakra()
            .BuildServiceProvider();

        var app = new App(services);
        app.InitializeComponent();
        return app.Run();
    }
}
