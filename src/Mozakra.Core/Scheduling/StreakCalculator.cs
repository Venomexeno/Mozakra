namespace Mozakra.Core.Scheduling;

/// <summary>
/// Streak = consecutive days with logged minutes > 0, counting back from yesterday (today counts once
/// logged, and an unlogged today doesn't break it). Days with 0 capacity are skipped when nothing was
/// logged, and count when something was.
/// </summary>
internal static class StreakCalculator
{
    public static int Compute(SchedulingContext ctx)
    {
        var loggedByDay = ctx.ActiveLogs
            .GroupBy(l => l.Date)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Minutes));
        var loggedDays = loggedByDay.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (loggedDays.Count == 0) return 0;

        var earliest = loggedDays.Min();
        int streak = Logged(loggedByDay, ctx.Today) ? 1 : 0;

        for (var day = ctx.Today.AddDays(-1); day >= earliest; day = day.AddDays(-1))
        {
            if (Logged(loggedByDay, day)) streak++;
            else if (ctx.Calendar.GetCapacity(day) > 0) break;
        }
        return streak;
    }

    private static bool Logged(Dictionary<DateOnly, int> loggedByDay, DateOnly day)
        => loggedByDay.GetValueOrDefault(day) > 0;
}
