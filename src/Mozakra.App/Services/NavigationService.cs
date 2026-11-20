using System.Windows;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using Mozakra.App.Localization;
using Mozakra.App.Views;

namespace Mozakra.App.Services;

/// <summary>
/// Opens the app's windows. The screens are normal windows (not desktop-pinned), one instance each:
/// opening one that is already open brings it to the front. The note goes through <see cref="NoteHost"/>.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly NoteHost _note;
    private readonly IServiceProvider _services;
    private readonly LocalizationService _localization;
    private readonly Dictionary<Type, Window> _open = [];

    public NavigationService(NoteHost note, IServiceProvider services, LocalizationService localization)
    {
        _note = note;
        _services = services;
        _localization = localization;
        _localization.LanguageChanged += (_, _) =>
        {
            foreach (var window in _open.Values) ApplyCulture(window);
        };
    }

    public void ShowNote() => _ = _note.ShowAsync();

    public void HideNote() => _ = _note.HideAsync();

    public void OpenPlans() => Open<PlansWindow>();

    /// <summary>Edits <paramref name="planId"/>, or adds a new plan when null. An open editor is reused as is.</summary>
    public void OpenPlanEditor(Guid? planId) => Open<PlanEditorWindow>(w => w.ViewModel.Load(planId));

    public void OpenFreeTime() => Open<FreeTimeWindow>();

    public void OpenSettings() => Open<SettingsWindow>();

    public void OpenOnboarding() => Open<OnboardingWindow>();

    private void Open<TWindow>(Action<TWindow>? initialize = null) where TWindow : Window
    {
        if (_open.TryGetValue(typeof(TWindow), out var existing))
        {
            BringToFront(existing);
            return;
        }

        var window = _services.GetRequiredService<TWindow>();
        initialize?.Invoke(window);
        ApplyCulture(window);
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Closed += (_, _) => _open.Remove(typeof(TWindow));
        _open[typeof(TWindow)] = window;
        window.Show();
        BringToFront(window);
    }

    /// <summary>Dates (DatePicker, formats) follow the UI language.</summary>
    private void ApplyCulture(Window window) =>
        window.Language = XmlLanguage.GetLanguage(_localization.Culture.IetfLanguageTag);

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        // Above other apps once, without staying always-on-top.
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
