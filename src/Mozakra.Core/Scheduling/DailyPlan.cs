namespace Mozakra.Core.Scheduling;

public sealed record DailyPlan(DateOnly Date, int CapacityMinutes, int TotalTargetMinutes, int LoggedMinutes,
                               IReadOnlyList<PlanTarget> Targets, Guid? MostUrgentPlanId);
