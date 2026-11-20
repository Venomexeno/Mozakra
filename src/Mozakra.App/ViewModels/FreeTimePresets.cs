namespace Mozakra.App.ViewModels;

/// <summary>Starting points for the weekly free time. The weekend follows the language (Fri+Sat in Arabic, Sat+Sun in English).</summary>
public static class FreeTimePresets
{
    public const string Student = "Student", Employee = "Employee", Freelancer = "Freelancer";

    /// <summary>Minutes per day, index = (int)DayOfWeek.</summary>
    public static int[] Build(string preset, bool arabicWeek)
    {
        var (weekday, weekend) = preset switch
        {
            Student => (120, 240),
            Employee => (60, 180),
            Freelancer => (180, 90),
            _ => (60, 120),
        };
        var minutes = new int[7];
        for (int day = 0; day < 7; day++)
            minutes[day] = IsWeekend((DayOfWeek)day, arabicWeek) ? weekend : weekday;
        return minutes;
    }

    public static bool IsWeekend(DayOfWeek day, bool arabicWeek) => arabicWeek
        ? day is DayOfWeek.Friday or DayOfWeek.Saturday
        : day is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Display order: Saturday first in Arabic, Monday first in English.</summary>
    public static IEnumerable<DayOfWeek> WeekOrder(bool arabicWeek)
    {
        var first = arabicWeek ? DayOfWeek.Saturday : DayOfWeek.Monday;
        for (int i = 0; i < 7; i++) yield return (DayOfWeek)(((int)first + i) % 7);
    }
}
