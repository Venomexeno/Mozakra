using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.Core.Models;

namespace Mozakra.App.ViewModels;

/// <summary>The free time window: the weekly grid + exceptions (vacations, free days, busy days). Saved with "Save".</summary>
public sealed partial class FreeTimeViewModel : ObservableObject, IDisposable
{
    private readonly AppStateService _state;
    private readonly IClock _clock;
    private readonly LocalizationService _localization;

    public FreeTimeViewModel(AppStateService state, IClock clock, LocalizationService localization, FreeTimeWeekViewModel week)
    {
        _state = state;
        _clock = clock;
        _localization = localization;
        Week = week;
        Week.Load(state.State.Weekly);
        foreach (var exception in state.State.Exceptions) Exceptions.Add(new FreeTimeExceptionViewModel(exception));
        UpdateHasExceptions();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Raised after a successful save (the window closes).</summary>
    public event EventHandler? Saved;

    public FreeTimeWeekViewModel Week { get; }
    public ObservableCollection<FreeTimeExceptionViewModel> Exceptions { get; } = [];

    [ObservableProperty] private bool _hasExceptions;
    [ObservableProperty] private string _errorText = "";

    [RelayCommand]
    private void AddException()
    {
        var today = _clock.Today;
        // Appended at the end: when ranges overlap, the newest one wins.
        Exceptions.Add(new FreeTimeExceptionViewModel(new AvailabilityException
        {
            From = today,
            To = today.AddDays(6),
            Minutes = 0,
            Label = FreeTimeExceptionViewModel.Vacation,
        }));
        UpdateHasExceptions();
    }

    [RelayCommand]
    private void RemoveException(FreeTimeExceptionViewModel? exception)
    {
        if (exception is null) return;
        Exceptions.Remove(exception);
        UpdateHasExceptions();
    }

    [RelayCommand]
    private void Save()
    {
        var weekly = new WeeklyAvailability();
        if (!Week.TryApply(weekly, out var error))
        {
            ErrorText = error!;
            return;
        }

        var exceptions = new List<AvailabilityException>();
        foreach (var row in Exceptions)
        {
            var model = row.ToModel(out error);
            if (model is null)
            {
                ErrorText = error!;
                return;
            }
            exceptions.Add(model);
        }

        ErrorText = "";
        _state.State.Weekly = weekly;
        _state.State.Exceptions = exceptions;
        _state.Save();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateHasExceptions() => HasExceptions = Exceptions.Count > 0;

    private void OnLanguageChanged(object? sender, EventArgs e) => Week.Reload();

    public void Dispose() => _localization.LanguageChanged -= OnLanguageChanged;
}
