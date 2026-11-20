namespace Mozakra.Core.Scheduling;

public sealed record PlanStatus(Guid PlanId, double ProgressPercent, int RemainingMinutes, PlanHealth Health,
                                DateOnly? ForecastFinish, DateOnly? SuggestedDeadline, int? ShortfallMinutes);
