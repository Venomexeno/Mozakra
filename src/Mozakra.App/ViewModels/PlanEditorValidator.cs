using Mozakra.App.Localization;
using Mozakra.Core.Models;

namespace Mozakra.App.ViewModels;

/// <summary>
/// Turns the editor's fields into a <see cref="StudyPlan"/> draft, collecting clear error messages.
/// The draft is null when there is any error.
/// </summary>
public static class PlanEditorValidator
{

    public static (StudyPlan? Draft, IReadOnlyList<string> Errors) Build(
        PlanEditorViewModel vm, StudyPlan? original, DateOnly today)
    {
        var errors = new List<string>();
        var plan = new StudyPlan
        {
            Id = original?.Id ?? vm.PlanId,
            Name = vm.Name.Trim(),
            Kind = vm.IsCourse ? PlanKind.Course : PlanKind.Work,
            ColorHex = vm.SelectedColorHex,
            StartsAfterPlanId = vm.SelectedStartsAfter?.Id,
            IsArchived = original?.IsArchived ?? false,
            CreatedAtUtc = original?.CreatedAtUtc ?? DateTime.UtcNow,
            // Keep the other kind's numbers so switching kind back and forth loses nothing.
            TotalVideos = original?.TotalVideos ?? 0,
            VideosDone = original?.VideosDone ?? 0,
            TotalVideoMinutes = original?.TotalVideoMinutes ?? 0,
            EstimatedMinutes = original?.EstimatedMinutes ?? 0,
            RemainingOverrideMinutes = original?.RemainingOverrideMinutes,
            RemainingOverrideSetOn = original?.RemainingOverrideSetOn,
            Checklist = vm.Checklist
                .Where(i => !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => new ChecklistItem { Id = i.Id, Title = i.Title.Trim(), Done = i.Done })
                .ToList(),
        };

        if (plan.Name.Length == 0) errors.Add(Loc.T("Screens.Editor.Error.Name"));

        if (plan.Kind == PlanKind.Course) ValidateCourse(vm, plan, errors);
        else ValidateWork(vm, plan, today, errors);

        if (vm.StartDate is not { } start) errors.Add(Loc.T("Screens.Editor.Error.Start"));
        else plan.StartDate = DateOnly.FromDateTime(start);

        if (!vm.NoDeadline)
        {
            if (vm.Deadline is not { } deadline) errors.Add(Loc.T("Screens.Editor.Error.DeadlineMissing"));
            else
            {
                plan.Deadline = DateOnly.FromDateTime(deadline);
                if (vm.StartDate is { } s && plan.Deadline < DateOnly.FromDateTime(s))
                    errors.Add(Loc.T("Screens.Editor.Error.DeadlineBeforeStart"));
            }
        }

        return (errors.Count == 0 ? plan : null, errors);
    }

    private static void ValidateCourse(PlanEditorViewModel vm, StudyPlan plan, List<string> errors)
    {
        if (!PlanEditorInput.TryInt(vm.VideosText, out var videos) || videos <= 0)
            errors.Add(Loc.T("Screens.Editor.Error.Videos"));
        else plan.TotalVideos = videos;

        int hours = 0, minutes = 0;
        bool hoursOk = vm.HoursText.Trim().Length == 0 || PlanEditorInput.TryInt(vm.HoursText, out hours);
        bool minutesOk = vm.MinutesText.Trim().Length == 0 || PlanEditorInput.TryInt(vm.MinutesText, out minutes);
        if (!hoursOk || !minutesOk || hours < 0 || minutes < 0 || hours * 60 + minutes <= 0)
            errors.Add(Loc.T("Screens.Editor.Error.Duration"));
        else plan.TotalVideoMinutes = hours * 60 + minutes;

        int done = 0;
        if (vm.VideosDoneText.Trim().Length > 0 && (!PlanEditorInput.TryInt(vm.VideosDoneText, out done) || done < 0))
            errors.Add(Loc.T("Screens.Editor.Error.VideosDone"));
        else if (plan.TotalVideos > 0 && done > plan.TotalVideos)
            errors.Add(Loc.T("Screens.Editor.Error.VideosDoneTooMany"));
        else plan.VideosDone = done;
    }

    private static void ValidateWork(PlanEditorViewModel vm, StudyPlan plan, DateOnly today, List<string> errors)
    {
        if (!PlanEditorInput.TryDouble(vm.EstimatedHoursText, out var hours) || hours <= 0 || hours > 100_000)
            errors.Add(Loc.T("Screens.Editor.Error.Estimate"));
        else plan.EstimatedMinutes = (int)Math.Round(hours * 60);

        if (!vm.IsEditing || vm.AdjustRemainingText.Trim().Length == 0) return;
        if (!PlanEditorInput.TryDouble(vm.AdjustRemainingText, out var remaining) || remaining < 0 || remaining > 100_000)
        {
            errors.Add(Loc.T("Screens.Editor.Error.Adjust"));
            return;
        }
        plan.RemainingOverrideMinutes = (int)Math.Round(remaining * 60);
        plan.RemainingOverrideSetOn = today;
    }
}
