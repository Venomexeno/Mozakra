using Microsoft.Extensions.DependencyInjection;
using Mozakra.App.Services;
using Mozakra.App.ViewModels;
using Mozakra.App.Views;
using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.App.Composition;

public static class ScreensFeature
{
    /// <summary>Dev/test only: points the app at another data folder instead of <c>%APPDATA%\Mozakra</c>.</summary>
    public const string DataDirectoryVariable = "MOZAKRA_DATA_DIR";

    /// <summary>
    /// Owned by Plan 06: plans, plan editor, free time, settings, onboarding and the real
    /// <see cref="INavigationService"/> (replaces the temporary one from Plan 04; last registration wins).
    /// </summary>
    public static IServiceCollection AddScreensFeature(this IServiceCollection services)
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            services.AddSingleton<IStateStore>(sp => new JsonStateStore(overrideDirectory, sp.GetRequiredService<IClock>()));

        services.AddSingleton(sp => new ExportImportService((JsonStateStore)sp.GetRequiredService<IStateStore>()));
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddTransient<PlansViewModel>();
        services.AddTransient<PlanEditorViewModel>();
        services.AddTransient<FreeTimeViewModel>();
        services.AddTransient<FreeTimeWeekViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<OnboardingViewModel>();

        services.AddTransient<PlansWindow>();
        services.AddTransient<PlanEditorWindow>();
        services.AddTransient<FreeTimeWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<OnboardingWindow>();
        return services;
    }
}
