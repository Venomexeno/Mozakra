using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Mozakra.App.Localization;
using Mozakra.App.Services;

namespace Mozakra.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services) => _services = services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = _services.GetRequiredService<AppStateService>().State.Settings;

        // First run (onboarding not finished yet): pick the language from Windows.
        if (!settings.OnboardingDone)
            settings.Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";
        _services.GetRequiredService<LocalizationService>().SetLanguage(settings.Language);

        _services.GetRequiredService<TrayService>().Initialize();
        _services.GetRequiredService<SingleInstanceService>().ListenForSecondInstance();

        var navigation = _services.GetRequiredService<INavigationService>();
        if (settings.OnboardingDone) navigation.ShowNote();
        else navigation.OpenOnboarding(); // Plan 06; the temporary navigation shows the note instead.

        _ = _services.GetRequiredService<UpdateService>().CheckAndApplyInBackgroundAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services.GetRequiredService<TrayService>().Dispose();
        _services.GetRequiredService<SingleInstanceService>().Dispose();
        base.OnExit(e);
    }
}
