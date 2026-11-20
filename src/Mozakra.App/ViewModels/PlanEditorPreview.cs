using System.Globalization;
using Mozakra.App.Localization;
using Mozakra.Core.Models;
using Mozakra.Core.Persistence;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>
/// Live preview of a plan draft: runs the scheduler on a copy of the state with the draft in it.
/// "≈ 35 min/day · finishes 12 Dec", or a warning with the earliest realistic date.
/// </summary>
public static class PlanEditorPreview
{
    public sealed record Result(string Text, bool IsWarning);

    public static Result Compute(AppState state, StudyPlan draft, IScheduler scheduler, DateOnly today, CultureInfo culture)
    {
        var copy = StateJson.Deserialize(StateJson.Serialize(state));
        copy.Plans.RemoveAll(p => p.Id == draft.Id);
        copy.Plans.Add(draft);

        var status = scheduler.GetStatuses(copy).FirstOrDefault(s => s.PlanId == draft.Id);
        if (status is null) return new Result(Loc.T("Screens.Editor.Preview.Archived"), false);

        string Date(DateOnly? d) => d?.ToString("d MMM yyyy", culture) ?? "—";

        switch (status.Health)
        {
            case PlanHealth.Completed:
                return new Result(Loc.T("Screens.Editor.Preview.Completed"), false);

            case PlanHealth.Impossible:
            {
                int needs = status.RemainingMinutes;
                int have = Math.Max(0, needs - (status.ShortfallMinutes ?? 0));
                return new Result(Loc.T("Screens.Editor.Preview.Impossible",
                    PlanEditorInput.Duration(needs), PlanEditorInput.Duration(have), Date(status.SuggestedDeadline)), true);
            }

            case PlanHealth.Overdue:
                return new Result(Loc.T("Screens.Editor.Preview.Overdue", Date(status.SuggestedDeadline)), true);
        }

        if (status.ForecastFinish is not { } finish)
            return new Result(Loc.T("Screens.Editor.Preview.NoFreeTime"), true);

        var from = draft.StartDate > today ? draft.StartDate : today;
        int perDay = AveragePerDay(copy, from, finish, status.RemainingMinutes);
        var text = Loc.T("Screens.Editor.Preview.OnTrack", PlanEditorInput.Duration(perDay), Date(finish));

        if (status.Health == PlanHealth.Waiting)
        {
            var after = copy.Plans.FirstOrDefault(p => p.Id == draft.StartsAfterPlanId)?.Name ?? "";
            text = Loc.T("Screens.Editor.Preview.Waiting", after) + " · " + text;
        }
        if (status.Health == PlanHealth.AtRisk)
            return new Result(text + " · " + Loc.T("Screens.Editor.Preview.Tight"), true);
        return new Result(text, false);
    }

    /// <summary>Remaining minutes spread over the days with free time between <paramref name="from"/> and the finish.</summary>
    private static int AveragePerDay(AppState state, DateOnly from, DateOnly finish, int remaining)
    {
        if (remaining <= 0) return 0;
        var calendar = new CapacityCalendar(state);
        int days = 0;
        for (var day = from; day <= finish; day = day.AddDays(1))
            if (calendar.GetCapacity(day) > 0) days++;
        return (int)Math.Ceiling((double)remaining / Math.Max(1, days));
    }
}
