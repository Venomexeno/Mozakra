using Microsoft.Extensions.DependencyInjection;
using Mozakra.App.Services;
using Mozakra.App.ViewModels;
using Mozakra.App.Views;

namespace Mozakra.App.Composition;

/// <summary>Plan 05: the note window, its ViewModel, dialogs and the tray's exit confirmation.</summary>
public static class NoteFeature
{
    public static IServiceCollection AddNoteFeature(this IServiceCollection services)
    {
        services.AddSingleton<INoteDialogs, NoteDialogs>();
        services.AddSingleton<NoteViewModel>();
        services.AddSingleton<NoteWindow>();
        services.AddSingleton<NoteExitConfirmation>(sp => new NoteExitConfirmation(
            sp.GetRequiredService<AppStateService>(),
            sp.GetRequiredService<Mozakra.Core.Scheduling.IScheduler>(),
            sp.GetRequiredService<Mozakra.Core.Models.IClock>(),
            sp.GetRequiredService<INoteDialogs>()));

        services.AddSingleton<NoteWindowFactory>(sp => () => sp.GetRequiredService<NoteWindow>());

        // Tray "Exit" asks first ("You still have 35 min today 😅"). Re-registering TrayService keeps its own
        // constructor and only adds the subscription, so the tray works the same with or without this feature.
        services.AddSingleton<TrayService>(sp =>
        {
            var tray = ActivatorUtilities.CreateInstance<TrayService>(sp);
            tray.ExitRequested += (_, _) => sp.GetRequiredService<NoteExitConfirmation>().Run();
            return tray;
        });
        return services;
    }
}
