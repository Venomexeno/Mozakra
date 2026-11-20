using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

/// <summary>
/// One snapshot of the state for a scheduling call: the active (non-archived) plans, their logs
/// and the capacity calendar. Archived plans and logs of unknown/archived plans are ignored everywhere.
/// </summary>
internal sealed class SchedulingContext
{
    private readonly Dictionary<Guid, StudyPlan> _activeById;
    private readonly Dictionary<Guid, List<LogEntry>> _logsByPlan;

    public SchedulingContext(AppState state, DateOnly today)
    {
        Today = today;
        Calendar = new CapacityCalendar(state);
        Plans = state.Plans.Where(p => !p.IsArchived).ToList();
        _activeById = Plans.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        _logsByPlan = state.Logs
            .Where(l => _activeById.ContainsKey(l.PlanId))
            .GroupBy(l => l.PlanId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public DateOnly Today { get; }
    public CapacityCalendar Calendar { get; }
    public IReadOnlyList<StudyPlan> Plans { get; }

    public IEnumerable<LogEntry> ActiveLogs => _logsByPlan.Values.SelectMany(l => l);

    public StudyPlan? Find(Guid id) => _activeById.GetValueOrDefault(id);

    public IReadOnlyList<LogEntry> LogsOf(Guid planId)
        => _logsByPlan.TryGetValue(planId, out var logs) ? logs : [];

    public int LoggedOn(Guid planId, DateOnly date) => LogsOf(planId).Where(l => l.Date == date).Sum(l => l.Minutes);

    public int LoggedOn(DateOnly date) => ActiveLogs.Where(l => l.Date == date).Sum(l => l.Minutes);

    public double Remaining(StudyPlan plan) => RemainingCalculator.RemainingExact(plan, LogsOf(plan.Id));

    public double RemainingAtStartOf(StudyPlan plan, DateOnly date)
        => RemainingCalculator.RemainingExact(plan, LogsOf(plan.Id), date);

    public bool IsComplete(StudyPlan plan) => RemainingCalculator.IsComplete(plan, LogsOf(plan.Id));

    /// <summary>The active plan this one starts after, or null (none, unknown, archived or itself).</summary>
    public StudyPlan? Predecessor(StudyPlan plan)
        => plan.StartsAfterPlanId is { } id && id != plan.Id ? Find(id) : null;

    /// <summary>Waiting = the plan it starts after isn't complete yet.</summary>
    public bool IsWaiting(StudyPlan plan) => Predecessor(plan) is { } pre && !IsComplete(pre);
}
