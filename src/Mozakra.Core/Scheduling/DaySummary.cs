namespace Mozakra.Core.Scheduling;

public sealed record DaySummary(DateOnly Date, int CapacityMinutes, int LoggedMinutes);
