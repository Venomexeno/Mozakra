using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

/// <summary>
/// Splits the user's free time across plans by deadline and need (DESIGN.md → Calculation rules).
/// Stateless: every call works on a fresh snapshot of <see cref="AppState"/>; "today" comes from <see cref="IClock"/>.
/// </summary>
public sealed class Scheduler : IScheduler
{
    private readonly IClock _clock;

    public Scheduler(IClock clock) => _clock = clock;

    public int GetCapacity(AppState state, DateOnly date) => new CapacityCalendar(state).GetCapacity(date);

    public DailyPlan GetDay(AppState state, DateOnly date)
    {
        var ctx = Context(state);
        var targets = TargetCalculator.Compute(ctx, date);
        var statuses = BuildStatuses(ctx, includeForecast: false);

        return new DailyPlan(
            date,
            ctx.Calendar.GetCapacity(date),
            targets.Sum(t => t.TargetMinutes),
            ctx.LoggedOn(date),
            targets,
            UrgencyRanker.MostUrgent(ctx, statuses, targets));
    }

    public IReadOnlyList<PlanStatus> GetStatuses(AppState state) => BuildStatuses(Context(state), includeForecast: true);

    public DateOnly? SuggestDeadline(AppState state, Guid planId)
    {
        var ctx = Context(state);
        return ctx.Find(planId) is { } plan ? FeasibilityChecker.SuggestDeadline(ctx, plan) : null;
    }

    public IReadOnlyList<DaySummary> GetWeek(AppState state, DateOnly weekStart)
    {
        var ctx = Context(state);
        return Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i))
            .Select(day => new DaySummary(day, ctx.Calendar.GetCapacity(day), ctx.LoggedOn(day)))
            .ToList();
    }

    public int GetStreak(AppState state) => StreakCalculator.Compute(Context(state));

    private SchedulingContext Context(AppState state) => new(state, _clock.Today);

    private static List<PlanStatus> BuildStatuses(SchedulingContext ctx, bool includeForecast)
    {
        var feasibility = FeasibilityChecker.Check(ctx);
        var forecast = includeForecast ? ForecastCalculator.Forecast(ctx) : null;
        return ctx.Plans.Select(plan => BuildStatus(ctx, plan, feasibility, forecast)).ToList();
    }

    private static PlanStatus BuildStatus(SchedulingContext ctx, StudyPlan plan,
                                          Dictionary<Guid, Feasibility> feasibility,
                                          Dictionary<Guid, DateOnly?>? forecast)
    {
        var logs = ctx.LogsOf(plan.Id);
        var (health, shortfall) = Health(ctx, plan, feasibility);
        bool needsNewDeadline = health is PlanHealth.Impossible or PlanHealth.Overdue;

        return new PlanStatus(
            plan.Id,
            RemainingCalculator.ProgressPercent(plan, logs),
            RemainingCalculator.Remaining(plan, logs),
            health,
            forecast?.GetValueOrDefault(plan.Id),
            needsNewDeadline ? FeasibilityChecker.SuggestDeadline(ctx, plan) : null,
            needsNewDeadline ? shortfall : null);
    }

    private static (PlanHealth Health, int? Shortfall) Health(SchedulingContext ctx, StudyPlan plan,
                                                             Dictionary<Guid, Feasibility> feasibility)
    {
        if (ctx.IsComplete(plan)) return (PlanHealth.Completed, null);
        if (ctx.IsWaiting(plan)) return (PlanHealth.Waiting, null);
        if (plan.Deadline is not { } deadline) return (PlanHealth.NoDeadline, null);

        int remaining = (int)Math.Ceiling(ctx.Remaining(plan));
        if (deadline < ctx.Today) return (PlanHealth.Overdue, remaining);

        return feasibility.TryGetValue(plan.Id, out var f)
            ? (f.Health, f.ShortfallMinutes)
            : (PlanHealth.OnTrack, null);
    }
}
