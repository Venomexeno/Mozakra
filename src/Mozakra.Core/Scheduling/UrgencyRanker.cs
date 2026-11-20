namespace Mozakra.Core.Scheduling;

/// <summary>
/// Most urgent plan = the non-waiting, non-complete plan with the worst health, then the nearest
/// deadline, then the largest target today.
/// </summary>
internal static class UrgencyRanker
{
    public static Guid? MostUrgent(SchedulingContext ctx, IReadOnlyList<PlanStatus> statuses,
                                   IReadOnlyList<PlanTarget> targets)
    {
        var targetById = targets.ToDictionary(t => t.PlanId, t => t.TargetMinutes);

        return statuses
            .Where(s => s.Health is not (PlanHealth.Waiting or PlanHealth.Completed))
            .Select(s => (Status: s, Plan: ctx.Find(s.PlanId)!))
            .OrderBy(x => Severity(x.Status.Health))
            .ThenBy(x => x.Plan.Deadline ?? DateOnly.MaxValue)
            .ThenByDescending(x => targetById.GetValueOrDefault(x.Status.PlanId))
            .Select(x => (Guid?)x.Status.PlanId)
            .FirstOrDefault();
    }

    private static int Severity(PlanHealth health) => health switch
    {
        PlanHealth.Overdue => 0,
        PlanHealth.Impossible => 1,
        PlanHealth.AtRisk => 2,
        PlanHealth.OnTrack => 3,
        _ => 4,   // NoDeadline
    };
}
