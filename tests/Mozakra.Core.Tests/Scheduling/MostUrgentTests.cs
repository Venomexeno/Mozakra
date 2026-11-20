using Mozakra.Core.Models;
using static Mozakra.Core.Tests.Scheduling.TestData;

namespace Mozakra.Core.Tests.Scheduling;

public class MostUrgentTests
{
    [Fact]
    public void WorstHealthWins_OverNearerDeadline()
    {
        var state = StateWithDailyCapacity(120);
        var onTrack = Work(300, Day(4));
        var impossible = Work(5000, Day(30));    // 5300 > 31 × 120
        state.Plans.AddRange([onTrack, impossible]);

        Assert.Equal(impossible.Id, Scheduler().GetDay(state, Today).MostUrgentPlanId);
    }

    [Fact]
    public void SameHealth_NearestDeadlineWins()
    {
        var state = StateWithDailyCapacity(120);
        var later = Work(300, Day(19));
        var sooner = Work(300, Day(9));
        state.Plans.AddRange([later, sooner]);

        Assert.Equal(sooner.Id, Scheduler().GetDay(state, Today).MostUrgentPlanId);
    }

    [Fact]
    public void SameHealthAndDeadline_LargestTargetWins()
    {
        var state = StateWithDailyCapacity(120);
        var small = Work(100, Day(9));
        var big = Work(400, Day(9));
        state.Plans.AddRange([small, big]);

        Assert.Equal(big.Id, Scheduler().GetDay(state, Today).MostUrgentPlanId);
    }

    [Fact]
    public void WaitingAndCompletedPlans_AreNeverMostUrgent()
    {
        var state = StateWithDailyCapacity(120);
        var done = Work(100, Day(-2));
        var blocker = Work(500);
        var waiting = Work(300, Day(-1));
        waiting.StartsAfterPlanId = blocker.Id;
        state.Plans.AddRange([done, waiting, blocker]);
        state.Logs.Add(Log(done, Day(-3), 100));

        Assert.Equal(blocker.Id, Scheduler().GetDay(state, Today).MostUrgentPlanId);
    }

    [Fact]
    public void NoActivePlans_MostUrgentIsNull()
    {
        Assert.Null(Scheduler().GetDay(StateWithDailyCapacity(120), Today).MostUrgentPlanId);
    }
}
