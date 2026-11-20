using Mozakra.Core.Models;
using static Mozakra.Core.Tests.Scheduling.TestData;

namespace Mozakra.Core.Tests.Scheduling;

public class SchedulerDayTests
{
    [Fact]
    public void SingleDeadlinePlan_TargetIsProportionalToTodaysCapacity()
    {
        // 600 remaining, 10 days × 120 = 1200 capacity → 600 × 120 / 1200 = 60
        var state = StateWithDailyCapacity(120);
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(120, day.CapacityMinutes);
        Assert.Equal(60, day.TargetOf(plan).TargetMinutes);
        Assert.Equal(60, day.TotalTargetMinutes);
    }

    [Fact]
    public void BiggerCapacityDay_GetsABiggerShare()
    {
        // today 240, the other 9 days 60 → 600 × 240 / (240 + 540) = 184.6
        var state = StateWithDailyCapacity(60);
        state.Exceptions.Add(new AvailabilityException { From = Today, To = Today, Minutes = 240 });
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);

        Assert.Equal(185, Scheduler().GetDay(state, Today).TargetOf(plan).TargetMinutes);
    }

    [Fact]
    public void MissedDay_RaisesTheNextDaysTarget()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);
        var scheduler = Scheduler();

        int yesterday = scheduler.GetDay(state, Day(-1)).TargetOf(plan).TargetMinutes;   // 600 × 120 / 1320
        int today = scheduler.GetDay(state, Today).TargetOf(plan).TargetMinutes;

        Assert.Equal(55, yesterday);
        Assert.Equal(60, today);
    }

    [Fact]
    public void ExtraLoggedMinutes_LowerTheNextDaysTarget()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);
        state.Logs.Add(Log(plan, Day(-1), 200));

        Assert.Equal(40, Scheduler().GetDay(state, Today).TargetOf(plan).TargetMinutes);   // 400 × 120 / 1200
    }

    [Fact]
    public void LoggingDuringTheDay_KeepsTodaysTargetStable()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);
        state.Logs.Add(Log(plan, Today, 30));

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(60, day.TargetOf(plan).TargetMinutes);
        Assert.Equal(30, day.TargetOf(plan).LoggedMinutes);
        Assert.Equal(30, day.LoggedMinutes);
    }

    [Fact]
    public void Course_TargetVideosIsCeilOfTargetOverMinutesPerVideo()
    {
        // 451.15 × 120 / 1200 = 45.1 → 45 min; 45 / 10.49 = 4.3 → 5 videos
        var state = StateWithDailyCapacity(120);
        var plan = Course(videosDone: 18, deadline: Day(9));
        state.Plans.Add(plan);

        var target = Scheduler().GetDay(state, Today).TargetOf(plan);

        Assert.Equal(45, target.TargetMinutes);
        Assert.Equal(5, target.TargetVideos);
    }

    [Fact]
    public void Work_HasNoTargetVideos()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);

        Assert.Null(Scheduler().GetDay(state, Today).TargetOf(plan).TargetVideos);
    }

    [Fact]
    public void SmallTarget_IsRoundedUpToFiveMinutes()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(20, Day(9));   // 2 minutes raw
        state.Plans.Add(plan);

        Assert.Equal(5, Scheduler().GetDay(state, Today).TargetOf(plan).TargetMinutes);
    }

    [Fact]
    public void ZeroCapacityDay_HasZeroTarget()
    {
        var state = StateWithDailyCapacity(120);
        state.Exceptions.Add(new AvailabilityException { From = Today, To = Today, Minutes = 0 });
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(0, day.CapacityMinutes);
        Assert.Equal(0, day.TargetOf(plan).TargetMinutes);
    }

    [Fact]
    public void NoDeadlinePlans_SplitTheLeftoverEqually()
    {
        var state = StateWithDailyCapacity(120);
        var deadlinePlan = Work(600, Day(9));    // takes 60
        var a = Work(1000);
        var b = Work(1000);
        state.Plans.AddRange([deadlinePlan, a, b]);

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(30, day.TargetOf(a).TargetMinutes);
        Assert.Equal(30, day.TargetOf(b).TargetMinutes);
        Assert.Equal(120, day.TotalTargetMinutes);
    }

    [Fact]
    public void NoDeadlinePlan_NeedingLessThanItsShare_LeavesTheRestToTheOthers()
    {
        var state = StateWithDailyCapacity(120);
        var small = Work(10);
        var big = Work(1000);
        state.Plans.AddRange([small, big]);

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(10, day.TargetOf(small).TargetMinutes);
        Assert.Equal(110, day.TargetOf(big).TargetMinutes);
    }

    [Fact]
    public void StartsAfter_DependentPlanGetsNothingUntilTheFirstIsComplete()
    {
        var state = StateWithDailyCapacity(120);
        var first = Work(300);
        var second = Work(300, Day(9));
        second.StartsAfterPlanId = first.Id;
        state.Plans.AddRange([first, second]);

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(0, day.TargetOf(second).TargetMinutes);
        Assert.Equal(120, day.TargetOf(first).TargetMinutes);
    }

    [Fact]
    public void StartsAfter_DependentPlanGetsTimeOnceTheFirstIsComplete()
    {
        var state = StateWithDailyCapacity(120);
        var first = Work(300);
        var second = Work(300, Day(9));
        second.StartsAfterPlanId = first.Id;
        state.Plans.AddRange([first, second]);
        state.Logs.Add(Log(first, Day(-1), 300));

        var day = Scheduler().GetDay(state, Today);

        Assert.Equal(30, day.TargetOf(second).TargetMinutes);   // 300 × 120 / 1200
        Assert.Equal(0, day.TargetOf(first).TargetMinutes);
    }

    [Fact]
    public void ArchivedPlans_AreIgnored()
    {
        var state = StateWithDailyCapacity(120);
        var archived = Work(600, Day(2));
        archived.IsArchived = true;
        var active = Work(1000);
        state.Plans.AddRange([archived, active]);
        state.Logs.Add(Log(archived, Today, 50));

        var scheduler = Scheduler();
        var day = scheduler.GetDay(state, Today);

        Assert.DoesNotContain(day.Targets, t => t.PlanId == archived.Id);
        Assert.Equal(120, day.TargetOf(active).TargetMinutes);
        Assert.Equal(0, day.LoggedMinutes);
        Assert.DoesNotContain(scheduler.GetStatuses(state), s => s.PlanId == archived.Id);
    }

    [Fact]
    public void GetWeek_ReturnsSevenDaysOfCapacityAndLoggedMinutes()
    {
        var state = StateWithDailyCapacity(60);
        state.Weekly.MinutesByDay[(int)DayOfWeek.Friday] = 0;
        var plan = Work(1000);
        state.Plans.Add(plan);
        state.Logs.Add(Log(plan, Day(1), 45));
        state.Logs.Add(Log(plan, Day(1), 15));

        var week = Scheduler().GetWeek(state, Today);

        Assert.Equal(7, week.Count);
        Assert.Equal(Today, week[0].Date);
        Assert.Equal(60, week[1].LoggedMinutes);
        Assert.Equal(0, week[4].CapacityMinutes);
        Assert.Equal(60, week[0].CapacityMinutes);
    }
}
