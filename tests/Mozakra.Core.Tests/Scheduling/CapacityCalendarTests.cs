using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;
using static Mozakra.Core.Tests.Scheduling.TestData;

namespace Mozakra.Core.Tests.Scheduling;

public class CapacityCalendarTests
{
    private static AppState WeeklyState()
    {
        var state = new AppState();
        state.Weekly.MinutesByDay[(int)DayOfWeek.Monday] = 90;
        state.Weekly.MinutesByDay[(int)DayOfWeek.Friday] = 240;
        return state;
    }

    [Fact]
    public void Capacity_UsesWeeklyValueForTheWeekday()
    {
        var calendar = new CapacityCalendar(WeeklyState());

        Assert.Equal(90, calendar.GetCapacity(Today));          // Monday
        Assert.Equal(240, calendar.GetCapacity(Day(4)));        // Friday
        Assert.Equal(0, calendar.GetCapacity(Day(1)));          // Tuesday
    }

    [Fact]
    public void Capacity_VacationExceptionIsZero()
    {
        var state = WeeklyState();
        state.Exceptions.Add(new AvailabilityException { From = Day(0), To = Day(6), Minutes = 0 });

        Assert.Equal(0, new CapacityCalendar(state).GetCapacity(Day(4)));
    }

    [Fact]
    public void Capacity_ExtraFreeDayExceptionGivesItsMinutes()
    {
        var state = WeeklyState();
        state.Exceptions.Add(new AvailabilityException { From = Day(1), To = Day(1), Minutes = 300 });

        var calendar = new CapacityCalendar(state);
        Assert.Equal(300, calendar.GetCapacity(Day(1)));
        Assert.Equal(90, calendar.GetCapacity(Day(0)));
    }

    [Fact]
    public void Capacity_OverlappingExceptions_LatestCreatedWins()
    {
        var state = WeeklyState();
        state.Exceptions.Add(new AvailabilityException { From = Day(0), To = Day(6), Minutes = 0 });
        state.Exceptions.Add(new AvailabilityException { From = Day(4), To = Day(4), Minutes = 60 });

        var calendar = new CapacityCalendar(state);
        Assert.Equal(60, calendar.GetCapacity(Day(4)));
        Assert.Equal(0, calendar.GetCapacity(Day(3)));
    }

    [Fact]
    public void Sum_IsInclusiveOverTheRange()
    {
        var calendar = new CapacityCalendar(WeeklyState());

        Assert.Equal(90 + 240, calendar.Sum(Day(0), Day(6)));
        Assert.Equal(90, calendar.Sum(Day(0), Day(0)));
        Assert.Equal(0, calendar.Sum(Day(3), Day(2)));
    }

    [Fact]
    public void Scheduler_GetCapacity_MatchesCalendar()
    {
        var state = WeeklyState();
        Assert.Equal(240, Scheduler().GetCapacity(state, Day(4)));
    }
}
