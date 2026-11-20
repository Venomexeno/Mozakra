using Microsoft.Extensions.DependencyInjection;
using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.App.Views;
using Mozakra.Core.Models;
using Mozakra.Core.Persistence;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.Composition;

public static class ServiceRegistration
{
    /// <summary>Everything the app needs. Feature registrations come last so they can override the defaults.</summary>
    public static IServiceCollection AddMozakra(this IServiceCollection services)
    {
        services.AddMozakraCore();
        services.AddMozakraAppServices();
        services.AddNoteFeature();     // Plan 05
        services.AddScreensFeature();  // Plan 06
        return services;
    }

    private static void AddMozakraCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        // %APPDATA%\Mozakra\state.json, with daily backups (Plan 02)
        // MOZAKRA_DATA_DIR (optional) points the app at another folder, e.g. a temp copy for manual testing.
        var dataDirectory = Environment.GetEnvironmentVariable("MOZAKRA_DATA_DIR") is { Length: > 0 } dir
            ? dir : JsonStateStore.DefaultDirectory;
        services.AddSingleton<IStateStore>(sp => new JsonStateStore(dataDirectory, sp.GetRequiredService<IClock>()));
        services.AddSingleton<IScheduler, Scheduler>();
    }

    private static void AddMozakraAppServices(this IServiceCollection services)
    {
        services.AddSingleton<AppStateService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<DesktopPinService>();
        services.AddSingleton<NoteAnimator>();
        services.AddSingleton<NoteHost>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<SingleInstanceService>();

        // Temporary: Plan 06 registers the real one in AddScreensFeature() (last registration wins).
        services.AddSingleton<INavigationService, NavigationService>();

        // The note window itself (NoteWindowFactory) is registered by AddNoteFeature().
    }
}
