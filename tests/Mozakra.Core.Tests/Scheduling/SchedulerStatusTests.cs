using Mozakra.Core.Scheduling;
using static Mozakra.Core.Tests.Scheduling.TestData;

namespace Mozakra.Core.Tests.Scheduling;

public class SchedulerStatusTests
{
    [Fact]
    public void TwoFeasibleDeadlinePlans_AreOnTrack()
    {
        var state = StateWithDailyCapacity(120);
        var a = Work(300, Day(4));    // 300 of 600
        var b = Work(300, Day(9));    // 600 of 1200
        state.Plans.AddRange([a, b]);

        var statuses = Scheduler().GetStatuses(state);

        Assert.Equal(PlanHealth.OnTrack, statuses.StatusOf(a).Health);
        Assert.Equal(PlanHealth.OnTrack, statuses.StatusOf(b).Health);
        Assert.Null(statuses.StatusOf(a).ShortfallMinutes);
    }

    [Fact]
    public void Overload_EarliestDeadlineGoesFirst_LaterPlanIsImpossibleWithShortfall()
    {
        // Late alone would fit (900 ≤ 1200), but Early (500 by day 4) comes first: 1400 > 1200.
        var state = StateWithDailyCapacity(120);
        var late = Work(900, Day(9), "late", createdOrder: 0);
        var early = Work(500, Day(4), "early", createdOrder: 1);
        state.Plans.AddRange([late, early]);

        var statuses = Scheduler().GetStatuses(state);

        Assert.Equal(PlanHealth.OnTrack, statuses.StatusOf(early).Health);
        Assert.Equal(PlanHealth.Impossible, statuses.StatusOf(late).Health);
        Assert.Equal(200, statuses.StatusOf(late).ShortfallMinutes);
    }

    [Fact]
    public void SinglePlanTooBig_IsImpossibleWithShortfall()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(1500, Day(9));
        state.Plans.Add(plan);

        var status = Scheduler().GetStatuses(state).StatusOf(plan);

        Assert.Equal(PlanHealth.Impossible, status.Health);
        Assert.Equal(300, status.ShortfallMinutes);
    }

    [Fact]
    public void Impossible_SuggestedDeadlineIsActuallyFeasible_AndTheDayBeforeIsNot()
    {
        var state = StateWithDailyCapacity(120);
        var late = Work(900, Day(9), "late", createdOrder: 0);
        var early = Work(500, Day(4), "early", createdOrder: 1);
        state.Plans.AddRange([late, early]);
        var scheduler = Scheduler();

        var suggested = scheduler.GetStatuses(state).StatusOf(late).SuggestedDeadline;

        Assert.Equal(Day(11), suggested);   // 1400 needs 12 days × 120
        Assert.Equal(suggested, scheduler.SuggestDeadline(state, late.Id));

        late.Deadline = suggested;
        Assert.NotEqual(PlanHealth.Impossible, scheduler.GetStatuses(state).StatusOf(late).Health);

        late.Deadline = suggested!.Value.AddDays(-1);
        Assert.Equal(PlanHealth.Impossible, scheduler.GetStatuses(state).StatusOf(late).Health);
    }

    [Fact]
    public void FeasibleButTight_IsAtRisk()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(1150, Day(9));    // 96% of 1200
        state.Plans.Add(plan);

        Assert.Equal(PlanHealth.AtRisk, Scheduler().GetStatuses(state).StatusOf(plan).Health);
    }

    [Fact]
    public void PastDeadlineAndNotComplete_IsOverdue_WithASuggestedDeadline()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(300, Day(-1));
        state.Plans.Add(plan);

        var status = Scheduler().GetStatuses(state).StatusOf(plan);

        Assert.Equal(PlanHealth.Overdue, status.Health);
        Assert.Equal(Day(2), status.SuggestedDeadline);   // 300 needs 3 days × 120
    }

    [Fact]
    public void CompletedPlan_IsCompletedAtFullProgress()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(100, Day(-3));
        state.Plans.Add(plan);
        state.Logs.Add(Log(plan, Day(-5), 100));

        var status = Scheduler().GetStatuses(state).StatusOf(plan);

        Assert.Equal(PlanHealth.Completed, status.Health);
        Assert.Equal(0, status.RemainingMinutes);
        Assert.Equal(100, status.ProgressPercent);
    }

    [Fact]
    public void WaitingPlan_IsWaiting_AndExcludedFromTheFeasibilityCheck()
    {
        var state = StateWithDailyCapacity(120);
        var first = Work(500, Day(4));
        var second = Work(5000, Day(5));
        second.StartsAfterPlanId = first.Id;
        state.Plans.AddRange([first, second]);

        var statuses = Scheduler().GetStatuses(state);

        Assert.Equal(PlanHealth.OnTrack, statuses.StatusOf(first).Health);
        Assert.Equal(PlanHealth.Waiting, statuses.StatusOf(second).Health);
    }

    [Fact]
    public void FeasibleDeadlinePlan_ForecastFinishesByTheDeadline()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(600, Day(9));
        state.Plans.Add(plan);

        var status = Scheduler().GetStatuses(state).StatusOf(plan);

        Assert.Equal(Day(9), status.ForecastFinish);
        Assert.Equal(600, status.RemainingMinutes);
    }

    [Fact]
    public void NoDeadlinePlan_ForecastUsesItsShareOfTheLeftover()
    {
        var state = StateWithDailyCapacity(120);
        var plan = Work(600);
        state.Plans.Add(plan);

        var status = Scheduler().GetStatuses(state).StatusOf(plan);

        Assert.Equal(PlanHealth.NoDeadline, status.Health);
        Assert.Equal(Day(4), status.ForecastFinish);   // 5 days × 120
    }

    [Fact]
    public void WaitingPlan_ForecastStartsAfterThePredecessorsForecastFinish()
    {
        var state = StateWithDailyCapacity(120);
        var first = Work(240);
        var second = Work(240);
        second.StartsAfterPlanId = first.Id;
        state.Plans.AddRange([first, second]);

        var statuses = Scheduler().GetStatuses(state);

        Assert.Equal(Day(1), statuses.StatusOf(first).ForecastFinish);
        Assert.Equal(Day(3), statuses.StatusOf(second).ForecastFinish);
    }

    [Fact]
    public void SuggestDeadline_UnknownOrArchivedPlan_IsNull()
    {
        var state = StateWithDailyCapacity(120);
        var archived = Work(100, Day(3));
        archived.IsArchived = true;
        state.Plans.Add(archived);

        var scheduler = Scheduler();
        Assert.Null(scheduler.SuggestDeadline(state, archived.Id));
        Assert.Null(scheduler.SuggestDeadline(state, Guid.NewGuid()));
    }
}
