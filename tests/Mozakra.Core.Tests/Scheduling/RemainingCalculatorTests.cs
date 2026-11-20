using Mozakra.Core.Scheduling;
using static Mozakra.Core.Tests.Scheduling.TestData;

namespace Mozakra.Core.Tests.Scheduling;

public class RemainingCalculatorTests
{
    [Fact]
    public void Course_RemainingMinutes_UsesVideoMinutesAndVideosDone()
    {
        // 640 × (1 − 18/61) = 451.15
        var plan = Course(videosDone: 18);

        Assert.Equal(451.15, RemainingCalculator.RemainingExact(plan, []), 2);
        Assert.Equal(451, RemainingCalculator.Remaining(plan, []));
    }

    [Fact]
    public void Course_MinutesPerVideo()
    {
        Assert.Equal(10.49, RemainingCalculator.MinutesPerVideo(Course()), 2);   // 640 / 61
    }

    [Fact]
    public void Course_Progress_IsVideosDoneOverTotal()
    {
        Assert.Equal(29.51, RemainingCalculator.ProgressPercent(Course(videosDone: 18), []), 2);
    }

    [Fact]
    public void Course_AllVideosDone_IsComplete()
    {
        var plan = Course(videosDone: 61);

        Assert.True(RemainingCalculator.IsComplete(plan, []));
        Assert.Equal(0, RemainingCalculator.Remaining(plan, []));
        Assert.Equal(100, RemainingCalculator.ProgressPercent(plan, []));
    }

    [Fact]
    public void Work_WithoutOverride_IsEstimateMinusAllLogs()
    {
        var plan = Work(600);
        var logs = new[] { Log(plan, Day(-5), 100), Log(plan, Day(-1), 50) };

        Assert.Equal(450, RemainingCalculator.Remaining(plan, logs));
        Assert.Equal(25, RemainingCalculator.ProgressPercent(plan, logs));
    }

    [Fact]
    public void Work_WithOverride_OnlyLogsOnOrAfterTheOverrideDateCount()
    {
        var plan = Work(600);
        plan.RemainingOverrideMinutes = 300;
        plan.RemainingOverrideSetOn = Day(-3);
        var logs = new[] { Log(plan, Day(-5), 100), Log(plan, Day(-3), 30), Log(plan, Day(-1), 20) };

        Assert.Equal(250, RemainingCalculator.Remaining(plan, logs));
    }

    [Fact]
    public void Work_RemainingNeverGoesBelowZero_AndThenIsComplete()
    {
        var plan = Work(100);
        var logs = new[] { Log(plan, Day(-1), 130) };

        Assert.Equal(0, RemainingCalculator.Remaining(plan, logs));
        Assert.True(RemainingCalculator.IsComplete(plan, logs));
    }

    [Fact]
    public void RemainingAtStartOfDay_AddsBackThatDaysLogs()
    {
        var work = Work(600);
        var workLogs = new[] { Log(work, Day(-1), 100), Log(work, Today, 40) };
        Assert.Equal(500, RemainingCalculator.RemainingExact(work, workLogs, Today));

        var course = Course(videosDone: 20);
        var courseLogs = new[] { Log(course, Today, 30, videos: 2) };
        Assert.Equal(451.15, RemainingCalculator.RemainingExact(course, courseLogs, Today), 2);
    }
}
