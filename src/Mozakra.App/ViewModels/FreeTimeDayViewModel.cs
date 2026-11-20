using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozakra.App.ViewModels;

/// <summary>One weekday: free minutes (slider + number) and an optional reminder time (stored; reminders ship in v1.1).</summary>
public sealed partial class FreeTimeDayViewModel : ObservableObject
{
    public const int MaxMinutes = 960;

    private bool _syncing;

    public FreeTimeDayViewModel(DayOfWeek day, string name)
    {
        Day = day;
        Name = name;
    }

    public DayOfWeek Day { get; }
    public string Name { get; }

    [ObservableProperty] private int _minutes;
    [ObservableProperty] private string _minutesText = "0";
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private string _reminderText = "";

    public event EventHandler? Changed;

    partial void OnMinutesChanged(int value)
    {
        int clamped = Math.Clamp(value, 0, MaxMinutes);
        if (clamped != value) { Minutes = clamped; return; }
        if (!_syncing)
        {
            _syncing = true;
            MinutesText = value.ToString();
            _syncing = false;
        }
        DurationText = PlanEditorInput.Duration(value);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnMinutesTextChanged(string value)
    {
        if (_syncing || !PlanEditorInput.TryInt(value, out var minutes)) return;
        _syncing = true;
        Minutes = Math.Clamp(minutes, 0, MaxMinutes);
        _syncing = false;
    }

    /// <summary>Re-reads the localized duration text (after a language change).</summary>
    public void RefreshTexts() => DurationText = PlanEditorInput.Duration(Minutes);
}
