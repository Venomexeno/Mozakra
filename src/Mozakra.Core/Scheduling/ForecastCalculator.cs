using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

/// <summary>
/// Forecast finish dates by simulating the coming days with the same rules as the daily targets:
/// deadline plans take their proportional share (earliest deadline first, never more than the day's
/// capacity), plans without a deadline split the leftover, and a waiting plan starts the day after
/// its predecessor's forecast finish. Today also counts what was already logged.
/// </summary>
internal static class ForecastCalculator
{
    public const int HorizonDays = 3650;
    private const double Done = 1e-6;

    public static Dictionary<Guid, DateOnly?> Forecast(SchedulingContext ctx)
    {
        var today = ctx.Today;
        var remaining = ctx.Plans.ToDictionary(p => p.Id, p => ctx.RemainingAtStartOf(p, today));
        var finish = ctx.Plans.ToDictionary(p => p.Id, _ => (DateOnly?)null);
        var doneBefore = new Dictionary<Guid, int>();   // plan id → day index it was finished (−1 = before today)
        foreach (var (id, rem) in remaining)
            if (rem <= Done) doneBefore[id] = -1;

        var deadlinePlans = ctx.Plans.Where(p => p.Deadline is not null)
            .OrderBy(p => p.Deadline).ThenBy(p => p.CreatedAtUtc).ToList();
        var noDeadlinePlans = ctx.Plans.Where(p => p.Deadline is null).ToList();
        var capacity = new CapacityPrefix(ctx.Calendar, today, HorizonDays + DaysUntilLastDeadline(ctx));

        for (int i = 0; i < HorizonDays && doneBefore.Count < ctx.Plans.Count; i++)
        {
            var day = today.AddDays(i);
            int dayCapacity = capacity.At(i);
            double left = dayCapacity;
            var given = new Dictionary<Guid, double>();

            foreach (var plan in deadlinePlans.Where(p => IsActive(ctx, p, i, day, doneBefore)))
            {
                double want = Want(plan, i, remaining[plan.Id], dayCapacity, capacity, today);
                given[plan.Id] = Give(ctx, plan, i, day, remaining[plan.Id], Math.Min(want, left));
                left = Math.Max(0, left - given[plan.Id]);
            }

            var sharing = noDeadlinePlans.Where(p => IsActive(ctx, p, i, day, doneBefore))
                .Select(p => (p.Id, remaining[p.Id]));
            foreach (var (id, share) in EqualSplit.Split(sharing, left))
                given[id] = Give(ctx, ctx.Find(id)!, i, day, remaining[id], share);

            foreach (var (id, minutes) in given)
            {
                remaining[id] -= minutes;
                if (remaining[id] <= Done)
                {
                    finish[id] = day;
                    doneBefore[id] = i;
                }
            }
        }
        return finish;
    }

    private static bool IsActive(SchedulingContext ctx, StudyPlan plan, int index, DateOnly day,
                                 Dictionary<Guid, int> doneBefore)
    {
        if (doneBefore.ContainsKey(plan.Id) || day < plan.StartDate) return false;
        if (index == 0) return !ctx.IsWaiting(plan);
        var pre = ctx.Predecessor(plan);
        return pre is null || (doneBefore.TryGetValue(pre.Id, out int doneAt) && doneAt < index);
    }

    private static double Want(StudyPlan plan, int index, double rem, int dayCapacity,
                               CapacityPrefix capacity, DateOnly today)
    {
        int deadlineIndex = plan.Deadline!.Value.DayNumber - today.DayNumber;
        if (deadlineIndex < index) return rem;                 // past its deadline: as much as possible
        int available = capacity.Sum(index, deadlineIndex);
        return available <= 0 ? rem : rem * dayCapacity / available;
    }

    /// <summary>Caps at what's left; today, what was already logged counts even beyond the plan.</summary>
    private static double Give(SchedulingContext ctx, StudyPlan plan, int index, DateOnly day, double rem, double planned)
    {
        double give = index == 0 ? Math.Max(planned, ctx.LoggedOn(plan.Id, day)) : planned;
        return Math.Clamp(give, 0, rem);
    }

    private static int DaysUntilLastDeadline(SchedulingContext ctx)
    {
        var last = ctx.Plans.Where(p => p.Deadline is not null).Select(p => p.Deadline!.Value).DefaultIfEmpty(ctx.Today).Max();
        return Math.Max(0, last.DayNumber - ctx.Today.DayNumber) + 1;
    }

    /// <summary>Capacity per day index (0 = today) with prefix sums for O(1) range totals.</summary>
    private sealed class CapacityPrefix
    {
        private readonly int[] _daily;
        private readonly long[] _prefix;

        public CapacityPrefix(CapacityCalendar calendar, DateOnly start, int days)
        {
            _daily = new int[days];
            _prefix = new long[days + 1];
            for (int i = 0; i < days; i++)
            {
                _daily[i] = calendar.GetCapacity(start.AddDays(i));
                _prefix[i + 1] = _prefix[i] + _daily[i];
            }
        }

        public int At(int index) => _daily[index];

        public int Sum(int from, int to)
            => (int)Math.Min(_prefix[Math.Min(to, _daily.Length - 1) + 1] - _prefix[from], int.MaxValue);
    }
}
