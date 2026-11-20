using Mozakra.Core.Models;

namespace Mozakra.Core.Scheduling;

/// <summary>
/// Free minutes per date. An exception covering the date wins over the weekly value; when exceptions
/// overlap, the latest-created one wins. Exceptions are appended to <see cref="AppState.Exceptions"/>
/// as they are created, so "latest created" = the last matching entry in the list.
/// </summary>
public sealed class CapacityCalendar
{
    private readonly AppState _state;

    public CapacityCalendar(AppState state) => _state = state;

    public int GetCapacity(DateOnly date)
    {
        var exceptions = _state.Exceptions;
        for (int i = exceptions.Count - 1; i >= 0; i--)
        {
            var ex = exceptions[i];
            if (ex.From <= date && date <= ex.To)
                return Math.Max(0, ex.Minutes);
        }

        var byDay = _state.Weekly.MinutesByDay;
        int index = (int)date.DayOfWeek;
        return byDay is not null && index < byDay.Length ? Math.Max(0, byDay[index]) : 0;
    }

    /// <summary>Total capacity from <paramref name="from"/> to <paramref name="to"/>, both inclusive.</summary>
    public int Sum(DateOnly from, DateOnly to)
    {
        long total = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            total += GetCapacity(d);
            if (d == DateOnly.MaxValue) break;
        }
        return (int)Math.Min(total, int.MaxValue);
    }
}
