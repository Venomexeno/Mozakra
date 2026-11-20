namespace Mozakra.Core.Models;

public sealed class WeeklyAvailability
{
    public int[] MinutesByDay { get; set; } = new int[7];          // index = (int)DayOfWeek (Sunday = 0)
    public TimeOnly?[] ReminderByDay { get; set; } = new TimeOnly?[7];
}
