namespace Mozakra.Core.Scheduling;

public sealed record PlanTarget(Guid PlanId, int TargetMinutes, int LoggedMinutes, int? TargetVideos);
