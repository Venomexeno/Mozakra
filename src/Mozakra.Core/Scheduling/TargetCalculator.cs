using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

/// <summary>
/// Daily targets. A deadline plan gets <c>remainingAtStartOfDay × capacity(day) / Σ capacity(day..deadline)</c>;
/// using the start-of-day remaining keeps the target stable while the user logs during the day and
/// redistributes missed or extra time over the days left. Plans without a deadline split the leftover.
/// </summary>
internal static class TargetCalculator
{
    public const int MinimumTargetMinutes = 5;

    public static IReadOnlyList<PlanTarget> Compute(SchedulingContext ctx, DateOnly date)
    {
        int capacity = ctx.Calendar.GetCapacity(date);
        var raw = new Dictionary<Guid, double>();
        var remaining = new Dictionary<Guid, double>();
        var noDeadline = new List<(Guid, double)>();

        foreach (var plan in ctx.Plans)
        {
            double rem = ctx.RemainingAtStartOf(plan, date);
            remaining[plan.Id] = rem;
            raw[plan.Id] = 0;

            if (capacity == 0 || rem <= 0 || date < plan.StartDate || ctx.IsWaiting(plan))
                continue;

            if (plan.Deadline is { } deadline)
                raw[plan.Id] = DeadlineShare(ctx, date, deadline, rem, capacity);
            else
                noDeadline.Add((plan.Id, rem));
        }

        double leftover = Math.Max(0, capacity - raw.Values.Sum());
        foreach (var (id, minutes) in EqualSplit.Split(noDeadline, leftover))
            raw[id] = minutes;

        return ctx.Plans.Select(plan => BuildTarget(ctx, plan, date, raw[plan.Id])).ToList();
    }

    /// <summary>What a deadline plan needs on <paramref name="date"/>; overdue plans take what the day allows.</summary>
    public static double DeadlineShare(SchedulingContext ctx, DateOnly date, DateOnly deadline, double rem, int capacity)
    {
        if (deadline < date) return Math.Min(rem, capacity);
        int available = ctx.Calendar.Sum(date, deadline);
        return available <= 0 ? Math.Min(rem, capacity) : rem * capacity / available;
    }

    /// <summary>Whole minutes; anything above 0 but under 5 shows as 5 so tiny targets stay meaningful.</summary>
    public static int RoundTarget(double rawMinutes)
    {
        if (rawMinutes <= 0) return 0;
        int rounded = (int)Math.Round(rawMinutes, MidpointRounding.AwayFromZero);
        return Math.Max(rounded, MinimumTargetMinutes);
    }

    private static PlanTarget BuildTarget(SchedulingContext ctx, StudyPlan plan, DateOnly date, double rawMinutes)
    {
        int target = RoundTarget(rawMinutes);
        int logged = ctx.LoggedOn(plan.Id, date);
        return new PlanTarget(plan.Id, target, logged, TargetVideos(ctx, plan, date, target));
    }

    private static int? TargetVideos(SchedulingContext ctx, StudyPlan plan, DateOnly date, int targetMinutes)
    {
        if (plan.Kind != PlanKind.Course) return null;
        double perVideo = RemainingCalculator.MinutesPerVideo(plan);
        if (targetMinutes == 0 || perVideo <= 0) return 0;

        int videosLeft = plan.TotalVideos - RemainingCalculator.VideosDone(plan, ctx.LogsOf(plan.Id), date);
        int videos = (int)Math.Ceiling(targetMinutes / perVideo - 1e-9);
        return Math.Clamp(videos, 0, Math.Max(0, videosLeft));
    }
}
