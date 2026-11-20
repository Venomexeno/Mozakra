using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>Details (toggle saved in settings): week bars with day editing, streak, per-plan status.</summary>
public sealed partial class NoteViewModel
{
    public const double WeekBarArea = 56;

    public ObservableCollection<NoteWeekDay> Week { get; } = [];
    public ObservableCollection<NotePlanStatusRow> Statuses { get; } = [];

    [ObservableProperty] private string _weekTitle = "";
    [ObservableProperty] private string _moreWaitingText = "";
    [ObservableProperty] private string _streakText = "";

    public bool ShowDetails
    {
        get => State.Settings.ShowDetails;
        set
        {
            if (State.Settings.ShowDetails == value) return;
            State.Settings.ShowDetails = value;
            OnPropertyChanged();
            _state.Save();
        }
    }

    [RelayCommand] private void ToggleDetails() => ShowDetails = !ShowDetails;

    /// <summary>Saturday for Arabic, Monday for English (same as the free-time screen).</summary>
    public DateOnly WeekStart(DateOnly day)
    {
        var first = _localization.IsRightToLeft ? DayOfWeek.Saturday : DayOfWeek.Monday;
        return day.AddDays(-(((int)day.DayOfWeek - (int)first + 7) % 7));
    }

    private void RefreshDetails(Dictionary<Guid, PlanStatus> statuses, List<StudyPlan> active)
    {
        OnPropertyChanged(nameof(ShowDetails));
        int streak = _scheduler.GetStreak(State);
        StreakText = streak > 0 ? Loc.T("Note.StreakFormat", streak) : Loc.T("Note.StreakNone");

        var days = _scheduler.GetWeek(State, WeekStart(Today));
        double max = Math.Max(60, days.Max(d => Math.Max(d.CapacityMinutes, d.LoggedMinutes)));
        Week.Clear();
        foreach (var d in days) Week.Add(BuildWeekDay(d, max));
        WeekTitle = Loc.T("Note.Week.Title",
            NoteFormat.Duration(days.Sum(d => Math.Max(0, d.LoggedMinutes))), NoteFormat.Duration(days.Sum(d => d.CapacityMinutes)));

        // Plans in progress plus the next waiting one; a long chain of waiting plans would make the note taller
        // than the screen (the Plans window lists them all).
        Statuses.Clear();
        int waitingShown = 0, waitingHidden = 0;
        foreach (var plan in active)
        {
            if (!statuses.TryGetValue(plan.Id, out var status)) continue;
            if (status.Health == PlanHealth.Waiting && waitingShown++ >= 1) { waitingHidden++; continue; }
            Statuses.Add(BuildStatusRow(plan, status, active));
        }
        MoreWaitingText = waitingHidden > 0 ? Loc.T("Note.Status.MoreWaiting", waitingHidden) : "";
    }

    private NoteWeekDay BuildWeekDay(DaySummary d, double max)
    {
        int logged = Math.Max(0, d.LoggedMinutes);
        string label = Application.Current?.TryFindResource("Note.WeekDays") is Array { Length: 7 } names
            ? names.GetValue((int)d.Date.DayOfWeek) as string ?? ""
            : Culture.DateTimeFormat.GetAbbreviatedDayName(d.Date.DayOfWeek);
        string tooltip = Loc.T("Note.Week.DayTooltip", NoteFormat.Date(d.Date, Culture, "Note.LongDateFormat"),
            NoteFormat.Duration(logged), NoteFormat.Duration(d.CapacityMinutes));
        return new NoteWeekDay(d.Date, label, logged > 0 ? logged.ToString() : "",
            WeekBarArea * logged / max, WeekBarArea * d.CapacityMinutes / max,
            d.Date == Today, d.Date > Today, d.CapacityMinutes > 0 && logged >= d.CapacityMinutes, tooltip);
    }

    private NotePlanStatusRow BuildStatusRow(StudyPlan plan, PlanStatus status, List<StudyPlan> active)
    {
        string percent = $"{Math.Floor(status.ProgressPercent)}%";
        string progress = plan.Kind == PlanKind.Course
            ? $"{Loc.T("Note.Status.Videos", plan.VideosDone, plan.TotalVideos)} · {percent}"
            : $"{percent} · {Loc.T("Note.Status.Left", NoteFormat.Duration(status.RemainingMinutes))}";

        string forecast = status.Health switch
        {
            PlanHealth.Completed => Loc.T("Note.Status.Completed"),
            PlanHealth.Waiting => Loc.T("Note.Status.Waiting",
                active.FirstOrDefault(p => p.Id == plan.StartsAfterPlanId)?.Name ?? ""),
            _ => (status.ForecastFinish, plan.Deadline) switch
            {
                ({ } f, { } dl) => Loc.T("Note.Status.ForecastDeadline", FormatDate(f), FormatDate(dl)),
                ({ } f, null) => Loc.T("Note.Status.Forecast", FormatDate(f)),
                (null, { } dl) => Loc.T("Note.Status.Deadline", FormatDate(dl)),
                _ => "",
            },
        };
        return new NotePlanStatusRow(plan.Id, plan.Name, plan.ColorHex, status.ProgressPercent, progress, forecast,
            NoteHealthText.Icon(status.Health));
    }

    [RelayCommand]
    private void EditDay(NoteWeekDay? day)
    {
        if (day is null) return;
        if (day.IsFuture)
        {
            ShowToast(Loc.T("Note.Toast.FutureDay"));
            return;
        }

        var rows = State.Plans.Where(p => !p.IsArchived)
            .Select(p => new NoteEditDayRow(p.Id, p.Name, p.ColorHex,
                State.Logs.Where(l => l.PlanId == p.Id && l.Date == day.Date).Sum(l => l.Minutes)))
            .ToList();
        if (rows.Count == 0) return;

        string date = NoteFormat.Date(day.Date, Culture, "Note.LongDateFormat");
        if (_dialogs.EditDay(Loc.T("Note.EditDay.Title", date), rows) is not { } result) return;
        var changes = result.Where(r => r.Minutes is { } m && m != r.OriginalMinutes).ToList();
        if (changes.Count == 0) return;

        Mutate(Loc.T("Note.Undo.EditDay", date), s =>
        {
            foreach (var row in changes)
                s.Logs.Add(new LogEntry
                {
                    PlanId = row.PlanId,
                    Date = day.Date,
                    Minutes = row.Minutes!.Value - row.OriginalMinutes,
                    CreatedAtUtc = _clock.UtcNow,
                });
        });
        int total = State.Logs.Where(l => l.Date == day.Date && rows.Any(r => r.PlanId == l.PlanId)).Sum(l => l.Minutes);
        ShowToast(Loc.T("Note.Toast.DayEdited", date, NoteFormat.Duration(total)));
    }
}
