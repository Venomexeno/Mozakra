using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.Core.Tests.Scheduling;

/// <summary>Small builders so each test reads as its rule.</summary>
internal static class TestData
{
    /// <summary>Monday, 16 November 2026.</summary>
    public static readonly DateOnly Today = new(2026, 11, 16);

    public static DateOnly Day(int offset) => Today.AddDays(offset);

    public static Scheduler Scheduler() => new(new FakeClock(Today));

    public static AppState StateWithDailyCapacity(int minutes)
    {
        var state = new AppState();
        Array.Fill(state.Weekly.MinutesByDay, minutes);
        return state;
    }

    public static StudyPlan Work(int estimatedMinutes, DateOnly? deadline = null, string name = "work",
                                 int createdOrder = 0)
        => new()
        {
            Name = name,
            Kind = PlanKind.Work,
            EstimatedMinutes = estimatedMinutes,
            Deadline = deadline,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(createdOrder),
        };

    /// <summary>61 videos, 640 video minutes.</summary>
    public static StudyPlan Course(int videosDone = 18, DateOnly? deadline = null)
        => new()
        {
            Name = "course",
            Kind = PlanKind.Course,
            TotalVideos = 61,
            TotalVideoMinutes = 640,
            VideosDone = videosDone,
            Deadline = deadline,
        };

    public static LogEntry Log(StudyPlan plan, DateOnly date, int minutes, int videos = 0)
        => new() { PlanId = plan.Id, Date = date, Minutes = minutes, VideosDone = videos };

    public static PlanStatus StatusOf(this IReadOnlyList<PlanStatus> statuses, StudyPlan plan)
        => statuses.Single(s => s.PlanId == plan.Id);

    public static PlanTarget TargetOf(this DailyPlan day, StudyPlan plan)
        => day.Targets.Single(t => t.PlanId == plan.Id);
}
