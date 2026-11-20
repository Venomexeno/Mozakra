using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>⚠️ Impossible/Overdue plans: needed vs available time + [Extend] / [Add free time] / [Leave it].</summary>
public sealed partial class NoteViewModel
{
    // "Leave it" hides a warning until tomorrow (in memory only).
    private readonly Dictionary<Guid, DateOnly> _dismissedOn = [];

    public ObservableCollection<NoteWarning> Warnings { get; } = [];

    private void RefreshWarnings(Dictionary<Guid, PlanStatus> statuses, List<StudyPlan> active)
    {
        Warnings.Clear();
        foreach (var plan in active)
        {
            if (!statuses.TryGetValue(plan.Id, out var status)) continue;
            if (status.Health is not (PlanHealth.Impossible or PlanHealth.Overdue)) continue;
            if (_dismissedOn.TryGetValue(plan.Id, out var day) && day == Today) continue;
            Warnings.Add(BuildWarning(plan, status));
        }
    }

    private NoteWarning BuildWarning(StudyPlan plan, PlanStatus status)
    {
        int needed = status.RemainingMinutes;
        string message = status.Health == PlanHealth.Overdue
            ? Loc.T("Note.Warning.Overdue", FormatDate(plan.Deadline), NoteFormat.Duration(needed))
            : Loc.T("Note.Warning.Impossible", NoteFormat.Duration(needed),
                NoteFormat.Duration(Math.Max(0, needed - (status.ShortfallMinutes ?? 0))), FormatDate(plan.Deadline));
        string extend = status.SuggestedDeadline is { } suggested
            ? Loc.T("Note.Warning.Extend", FormatDate(suggested))
            : "";
        return new NoteWarning(plan.Id, $"⚠️ {plan.Name}", message, status.SuggestedDeadline, extend);
    }

    private string FormatDate(DateOnly? date) => date is { } d ? NoteFormat.Date(d, Culture) : "";

    [RelayCommand]
    private void ExtendDeadline(NoteWarning? warning)
    {
        if (warning?.SuggestedDeadline is not { } deadline) return;
        var plan = State.Plans.FirstOrDefault(p => p.Id == warning.PlanId);
        if (plan is null) return;
        Mutate(Loc.T("Note.Undo.Extend", plan.Name), s =>
        {
            var target = s.Plans.First(p => p.Id == warning.PlanId);
            target.Deadline = deadline;
        });
        ShowToast(Loc.T("Note.Toast.Extended", plan.Name, FormatDate(deadline)));
    }

    [RelayCommand]
    private void AddFreeTime() => _navigation.OpenFreeTime();

    [RelayCommand]
    private void LeaveWarning(NoteWarning? warning)
    {
        if (warning is null) return;
        _dismissedOn[warning.PlanId] = Today;
        Warnings.Remove(warning);
    }
}
