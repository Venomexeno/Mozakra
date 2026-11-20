using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.App.Services;

namespace Mozakra.App.ViewModels;

/// <summary>First-run wizard: 1 language → 2 free time (presets + weekly grid) → 3 first plan (or skip).</summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    public const int LastStep = 2;

    private readonly AppStateService _state;
    private readonly LocalizationService _localization;
    private readonly INavigationService _navigation;
    private bool _loading = true;

    public OnboardingViewModel(AppStateService state, LocalizationService localization, INavigationService navigation,
        FreeTimeWeekViewModel week, PlanEditorViewModel editor)
    {
        _state = state;
        _localization = localization;
        _navigation = navigation;
        Week = week;
        Editor = editor;
        Week.Load(state.State.Weekly);
        IsArabic = localization.Language == "ar";
        _loading = false;
        UpdateStepTexts();
    }

    /// <summary>Raised when the wizard is finished or skipped (the window closes).</summary>
    public event EventHandler? Completed;

    public FreeTimeWeekViewModel Week { get; }
    public PlanEditorViewModel Editor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLanguageStep), nameof(IsFreeTimeStep), nameof(IsPlanStep), nameof(IsNotPlanStep), nameof(CanGoBack))]
    private int _step;

    public bool IsLanguageStep => Step == 0;
    public bool IsFreeTimeStep => Step == 1;
    public bool IsPlanStep => Step == LastStep;
    public bool IsNotPlanStep => !IsPlanStep;
    public bool CanGoBack => Step > 0;

    [ObservableProperty] private string _stepText = "";
    [ObservableProperty] private string _errorText = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEnglish))] private bool _isArabic;
    public bool IsEnglish { get => !IsArabic; set => IsArabic = !value; }

    partial void OnIsArabicChanged(bool value)
    {
        if (_loading) return;
        var language = value ? "ar" : "en";
        _localization.SetLanguage(language);
        _state.State.Settings.Language = language;
        _state.Save();
        Week.Reload();
        UpdateStepTexts();
    }

    partial void OnStepChanged(int value) => UpdateStepTexts();

    [RelayCommand]
    private void Next()
    {
        ErrorText = "";
        if (IsFreeTimeStep)
        {
            if (!Week.TryApply(_state.State.Weekly, out var error))
            {
                ErrorText = error!;
                return;
            }
            _state.Save();
            Editor.Load(null);   // fresh form; the preview now uses the new free time
        }
        if (Step < LastStep) Step++;
    }

    [RelayCommand]
    private void Back()
    {
        ErrorText = "";
        if (Step > 0) Step--;
    }

    /// <summary>Saves the first plan, then finishes. Stays on the step when the plan is invalid.</summary>
    [RelayCommand]
    private void Finish()
    {
        if (Editor.TrySave()) Complete();
    }

    [RelayCommand]
    private void Skip() => Complete();

    /// <summary>The wizard was closed: show the note anyway (it comes back on the next start).</summary>
    public void ShowNoteWithoutFinishing() => _navigation.ShowNote();

    private void Complete()
    {
        _state.State.Settings.OnboardingDone = true;
        _state.Save();
        Completed?.Invoke(this, EventArgs.Empty);
        _navigation.ShowNote();
    }

    private void UpdateStepTexts() => StepText = Loc.T("Screens.Onboarding.StepFormat", Step + 1, LastStep + 1);
}
