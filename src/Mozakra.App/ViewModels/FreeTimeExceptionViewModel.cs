using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mozakra.App.Localization;
using Mozakra.Core.Models;

namespace Mozakra.App.ViewModels;

/// <summary>An editable date range with its own free minutes (vacation = 0, free day, busy day).</summary>
public sealed partial class FreeTimeExceptionViewModel : ObservableObject
{
    public const string Vacation = "Vacation", FreeDay = "FreeDay", BusyDay = "BusyDay";

    private bool _loading;

    public FreeTimeExceptionViewModel(AvailabilityException source)
    {
        _loading = true;
        Id = source.Id;
        Labels.Add(new FreeTimeLabelOption(Vacation, Loc.T("Screens.FreeTime.Label.Vacation"), 0));
        Labels.Add(new FreeTimeLabelOption(FreeDay, Loc.T("Screens.FreeTime.Label.FreeDay"), 360));
        Labels.Add(new FreeTimeLabelOption(BusyDay, Loc.T("Screens.FreeTime.Label.BusyDay"), 30));
        var label = Labels.FirstOrDefault(l => l.Key == source.Label);
        if (label is null && !string.IsNullOrWhiteSpace(source.Label))
            Labels.Add(label = new FreeTimeLabelOption(source.Label, source.Label, null));
        SelectedLabel = label ?? (source.Minutes == 0 ? Labels[0] : Labels[1]);
        From = source.From.ToDateTime(TimeOnly.MinValue);
        To = source.To.ToDateTime(TimeOnly.MinValue);
        MinutesText = source.Minutes.ToString();
        _loading = false;
    }

    public Guid Id { get; }
    public ObservableCollection<FreeTimeLabelOption> Labels { get; } = [];

    [ObservableProperty] private DateTime? _from;
    [ObservableProperty] private DateTime? _to;
    [ObservableProperty] private string _minutesText = "0";
    [ObservableProperty] private FreeTimeLabelOption? _selectedLabel;

    /// <summary>Picking a type fills its usual minutes (the user can still change them).</summary>
    partial void OnSelectedLabelChanged(FreeTimeLabelOption? value)
    {
        if (!_loading && value?.DefaultMinutes is { } minutes) MinutesText = minutes.ToString();
    }

    partial void OnFromChanged(DateTime? value)
    {
        if (!_loading && value is { } from && (To is null || To < from)) To = from;
    }

    /// <summary>The model, or null with an error message when a field is invalid.</summary>
    public AvailabilityException? ToModel(out string? error)
    {
        error = null;
        if (From is not { } from || To is not { } to)
            error = Loc.T("Screens.FreeTime.Error.Dates");
        else if (to < from)
            error = Loc.T("Screens.FreeTime.Error.Range");
        else if (!PlanEditorInput.TryInt(MinutesText, out var minutes) || minutes < 0 || minutes > 1440)
            error = Loc.T("Screens.FreeTime.Error.Minutes");
        else
            return new AvailabilityException
            {
                Id = Id,
                From = DateOnly.FromDateTime(from),
                To = DateOnly.FromDateTime(to),
                Minutes = minutes,
                Label = SelectedLabel?.Key,
            };
        return null;
    }
}
