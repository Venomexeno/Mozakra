using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

/// <summary>
/// Remaining work, progress and completion for a single plan (see DESIGN.md → Calculation rules).
/// <paramref name="logs"/> are the plan's own log entries.
/// </summary>
public static class RemainingCalculator
{
    private const double Epsilon = 1e-9;

    /// <summary>Course: video minutes / total videos. 0 for work plans.</summary>
    public static double MinutesPerVideo(StudyPlan plan)
        => plan.Kind == PlanKind.Course && plan.TotalVideos > 0
            ? (double)plan.TotalVideoMinutes / plan.TotalVideos
            : 0;

    /// <summary>Remaining minutes, rounded to whole minutes.</summary>
    public static int Remaining(StudyPlan plan, IEnumerable<LogEntry> logs, DateOnly? startOfDay = null)
        => (int)Math.Round(RemainingExact(plan, logs, startOfDay), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Exact remaining minutes. With <paramref name="startOfDay"/>, the work logged on/after that date is
    /// treated as not done yet (the "remaining at start of day" used for the daily target).
    /// </summary>
    public static double RemainingExact(StudyPlan plan, IEnumerable<LogEntry> logs, DateOnly? startOfDay = null)
        => plan.Kind == PlanKind.Course
            ? CourseRemaining(plan, logs, startOfDay)
            : WorkRemaining(plan, logs, startOfDay);

    /// <summary>Videos done before <paramref name="startOfDay"/> (or now, when null).</summary>
    public static int VideosDone(StudyPlan plan, IEnumerable<LogEntry> logs, DateOnly? startOfDay = null)
    {
        int done = plan.VideosDone;
        if (startOfDay is { } day)
            done -= logs.Where(l => l.Date >= day).Sum(l => l.VideosDone);
        return Math.Clamp(done, 0, Math.Max(0, plan.TotalVideos));
    }

    public static bool IsComplete(StudyPlan plan, IEnumerable<LogEntry> logs)
        => RemainingExact(plan, logs) <= Epsilon;

    public static double ProgressPercent(StudyPlan plan, IEnumerable<LogEntry> logs)
    {
        if (plan.Kind == PlanKind.Course)
            return plan.TotalVideos > 0 ? 100.0 * VideosDone(plan, logs) / plan.TotalVideos : 0;

        double done = Math.Max(0, logs.Sum(l => l.Minutes));
        double remaining = RemainingExact(plan, logs);
        double total = done + remaining;
        if (total <= Epsilon) return IsComplete(plan, logs) ? 100 : 0;
        return Math.Clamp(100.0 * done / total, 0, 100);
    }

    private static double CourseRemaining(StudyPlan plan, IEnumerable<LogEntry> logs, DateOnly? startOfDay)
    {
        if (plan.TotalVideos <= 0) return 0;
        int done = VideosDone(plan, logs, startOfDay);
        return plan.TotalVideoMinutes * (1 - (double)done / plan.TotalVideos);
    }

    private static double WorkRemaining(StudyPlan plan, IEnumerable<LogEntry> logs, DateOnly? startOfDay)
    {
        var counted = startOfDay is { } day ? logs.Where(l => l.Date < day) : logs;

        double remaining;
        if (plan.RemainingOverrideMinutes is { } overrideMinutes)
        {
            var setOn = plan.RemainingOverrideSetOn ?? DateOnly.MinValue;
            remaining = overrideMinutes - counted.Where(l => l.Date >= setOn).Sum(l => l.Minutes);
        }
        else
        {
            remaining = plan.EstimatedMinutes - counted.Sum(l => l.Minutes);
        }
        return Math.Max(0, remaining);
    }
}
