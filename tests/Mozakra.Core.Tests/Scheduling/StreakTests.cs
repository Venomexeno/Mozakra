using Mozakra.Core.Models;
using static Mozakra.Core.Tests.Scheduling.TestData;

namespace Mozakra.Core.Tests.Scheduling;

public class StreakTests
{
    private static (AppState State, StudyPlan Plan) Setup()
    {
        var state = StateWithDailyCapacity(60);
        var plan = Work(10_000);
        state.Plans.Add(plan);
        return (state, plan);
    }

    [Fact]
    public void ConsecutiveLoggedDays_IncludingToday()
    {
        var (state, plan) = Setup();
        state.Logs.AddRange([Log(plan, Today, 10), Log(plan, Day(-1), 10), Log(plan, Day(-2), 10)]);

        Assert.Equal(3, Scheduler().GetStreak(state));
    }

    [Fact]
    public void TodayNotLoggedYet_DoesNotBreakTheStreak()
    {
        var (state, plan) = Setup();
        state.Logs.AddRange([Log(plan, Day(-1), 10), Log(plan, Day(-2), 10)]);

        Assert.Equal(2, Scheduler().GetStreak(state));
    }

    [Fact]
    public void MissedNormalDay_BreaksTheStreak()
    {
        var (state, plan) = Setup();
        state.Logs.AddRange([Log(plan, Day(-1), 10), Log(plan, Day(-3), 10), Log(plan, Day(-4), 10)]);

        Assert.Equal(1, Scheduler().GetStreak(state));
    }

    [Fact]
    public void ZeroCapacityDayWithoutLog_IsSkipped()
    {
        var (state, plan) = Setup();
        state.Exceptions.Add(new AvailabilityException { From = Day(-2), To = Day(-2), Minutes = 0 });
        state.Logs.AddRange([Log(plan, Day(-1), 10), Log(plan, Day(-3), 10)]);

        Assert.Equal(2, Scheduler().GetStreak(state));
    }

    [Fact]
    public void LogOnZeroCapacityDay_Counts()
    {
        var (state, plan) = Setup();
        state.Exceptions.Add(new AvailabilityException { From = Day(-2), To = Day(-2), Minutes = 0 });
        state.Logs.AddRange([Log(plan, Day(-1), 10), Log(plan, Day(-2), 25), Log(plan, Day(-3), 10)]);

        Assert.Equal(3, Scheduler().GetStreak(state));
    }

    [Fact]
    public void NoLogs_IsZero()
    {
        var (state, _) = Setup();
        Assert.Equal(0, Scheduler().GetStreak(state));
    }
}
