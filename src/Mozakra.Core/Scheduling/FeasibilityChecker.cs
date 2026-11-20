using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

internal sealed record Feasibility(PlanHealth Health, int? ShortfallMinutes);

/// <summary>
/// Earliest-deadline-first check: for each deadline plan (in deadline order), the cumulative demand of it
/// and every plan before it must fit in the capacity from today up to its deadline. Waiting plans are
/// excluded. Demand uses the start-of-day remaining, matching the capacity counted from today.
/// </summary>
internal static class FeasibilityChecker
{
    public const double AtRiskRatio = 0.9;
    public const int MaxSearchDays = 3650;

    public static Dictionary<Guid, Feasibility> Check(SchedulingContext ctx)
    {
        var result = new Dictionary<Guid, Feasibility>();
        long cumulativeDemand = 0;

        foreach (var (plan, demand) in Competing(ctx))
        {
            cumulativeDemand += demand;
            var deadline = plan.Deadline!.Value;

            if (deadline < ctx.Today)
            {
                result[plan.Id] = new Feasibility(PlanHealth.Overdue, demand);
                continue;
            }

            int capacity = ctx.Calendar.Sum(ctx.Today, deadline);
            if (cumulativeDemand > capacity)
                result[plan.Id] = new Feasibility(PlanHealth.Impossible, (int)(cumulativeDemand - capacity));
            else if (cumulativeDemand > AtRiskRatio * capacity)
                result[plan.Id] = new Feasibility(PlanHealth.AtRisk, null);
            else
                result[plan.Id] = new Feasibility(PlanHealth.OnTrack, null);
        }
        return result;
    }

    /// <summary>
    /// Earliest date D (from today) where the plan fits if its deadline were D: its own remaining, the
    /// remaining of the plans it waits on, and every competing plan due on/before D must fit in
    /// Σ capacity(today..D). Null when complete, or when it doesn't fit within ~10 years.
    /// </summary>
    public static DateOnly? SuggestDeadline(SchedulingContext ctx, StudyPlan plan)
    {
        long own = Demand(ctx, plan);
        if (own <= 0) return null;

        var chain = PredecessorChain(ctx, plan);
        long chainDemand = chain.Sum(p => (long)Demand(ctx, p));
        var others = Competing(ctx)
            .Where(c => c.Plan.Id != plan.Id && !chain.Contains(c.Plan))
            .ToList();   // already in deadline order

        long capacity = 0, othersDue = 0;
        int next = 0;
        for (int i = 0; i < MaxSearchDays; i++)
        {
            var day = ctx.Today.AddDays(i);
            capacity += ctx.Calendar.GetCapacity(day);
            while (next < others.Count && others[next].Plan.Deadline <= day)
                othersDue += others[next++].Demand;

            if (capacity >= own + chainDemand + othersDue)
                return day;
        }
        return null;
    }

    /// <summary>Deadline plans that need time today and aren't waiting, earliest deadline first.</summary>
    private static List<(StudyPlan Plan, int Demand)> Competing(SchedulingContext ctx)
        => ctx.Plans
            .Where(p => p.Deadline is not null && !ctx.IsWaiting(p))
            .Select(p => (Plan: p, Demand: Demand(ctx, p)))
            .Where(x => x.Demand > 0)
            .OrderBy(x => x.Plan.Deadline)
            .ThenBy(x => x.Plan.CreatedAtUtc)
            .ToList();

    private static int Demand(SchedulingContext ctx, StudyPlan plan)
        => (int)Math.Round(ctx.RemainingAtStartOf(plan, ctx.Today), MidpointRounding.AwayFromZero);

    private static HashSet<StudyPlan> PredecessorChain(SchedulingContext ctx, StudyPlan plan)
    {
        var chain = new HashSet<StudyPlan>();
        for (var pre = ctx.Predecessor(plan); pre is not null && pre != plan && chain.Add(pre); pre = ctx.Predecessor(pre))
        {
        }
        return chain;
    }
}
