using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.Core.Models;

namespace Mozakra.App.ViewModels;

/// <summary>The weekly grid (7 days, presets, weekly total). Shared by the free time window and the wizard.</summary>
public sealed partial class FreeTimeWeekViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    public FreeTimeWeekViewModel(LocalizationService localization) => _localization = localization;

    public ObservableCollection<FreeTimeDayViewModel> Days { get; } = [];

    [ObservableProperty] private string _totalText = "";

    private bool ArabicWeek => _localization.Language == "ar";

    /// <summary>Builds the rows (in the current language's week order) from <paramref name="weekly"/>.</summary>
    public void Load(WeeklyAvailability weekly)
    {
        var names = _localization.Culture.DateTimeFormat.DayNames;
        foreach (var day in Days) day.Changed -= OnDayChanged;
        Days.Clear();
        foreach (var dayOfWeek in FreeTimePresets.WeekOrder(ArabicWeek))
        {
            int index = (int)dayOfWeek;
            var day = new FreeTimeDayViewModel(dayOfWeek, names[index])
            {
                Minutes = weekly.MinutesByDay.ElementAtOrDefault(index),
                ReminderText = weekly.ReminderByDay.ElementAtOrDefault(index)?.ToString("HH:mm") ?? "",
            };
            day.RefreshTexts();
            day.Changed += OnDayChanged;
            Days.Add(day);
        }
        UpdateTotal();
    }

    /// <summary>Re-orders and re-labels the rows after a language change, keeping the edits.</summary>
    public void Reload()
    {
        var weekly = new WeeklyAvailability();
        foreach (var day in Days)
        {
            weekly.MinutesByDay[(int)day.Day] = day.Minutes;
            if (PlanEditorInput.TryTime(day.ReminderText, out var time)) weekly.ReminderByDay[(int)day.Day] = time;
        }
        Load(weekly);
    }

    /// <summary>Validates the reminder times; on success writes everything into <paramref name="weekly"/>.</summary>
    public bool TryApply(WeeklyAvailability weekly, out string? error)
    {
        var reminders = new TimeOnly?[7];
        foreach (var day in Days)
        {
            if (!PlanEditorInput.TryTime(day.ReminderText, out var time))
            {
                error = Loc.T("Screens.FreeTime.Error.Reminder", day.Name);
                return false;
            }
            reminders[(int)day.Day] = time;
        }
        weekly.MinutesByDay = new int[7];
        foreach (var day in Days) weekly.MinutesByDay[(int)day.Day] = day.Minutes;
        weekly.ReminderByDay = reminders;
        error = null;
        return true;
    }

    [RelayCommand]
    private void ApplyPreset(string? preset)
    {
        if (preset is null) return;
        var minutes = FreeTimePresets.Build(preset, ArabicWeek);
        foreach (var day in Days) day.Minutes = minutes[(int)day.Day];
    }

    private void OnDayChanged(object? sender, EventArgs e) => UpdateTotal();

    private void UpdateTotal() =>
        TotalText = Loc.T("Screens.FreeTime.WeeklyTotal", PlanEditorInput.Duration(Days.Sum(d => d.Minutes)));
}
