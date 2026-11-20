using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

public interface IScheduler
{
    int GetCapacity(AppState state, DateOnly date);
    DailyPlan GetDay(AppState state, DateOnly date);                // normally today
    IReadOnlyList<PlanStatus> GetStatuses(AppState state);
    DateOnly? SuggestDeadline(AppState state, Guid planId);        // earliest feasible finish date
    IReadOnlyList<DaySummary> GetWeek(AppState state, DateOnly weekStart);
    int GetStreak(AppState state);
}
