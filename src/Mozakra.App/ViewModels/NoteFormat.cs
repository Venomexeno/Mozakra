using System.Globalization;
using System.Windows;
using Mozakra.App.Localization;

namespace Mozakra.App.ViewModels;

/// <summary>Formatting helpers for the note (durations, dates, the rotating nudge line).</summary>
public static class NoteFormat
{
    /// <summary>"35 min", "2 h", "1 h 20 min" in the current language.</summary>
    public static string Duration(int minutes)
    {
        string sign = minutes < 0 ? "−" : "";
        minutes = Math.Abs(minutes);
        if (minutes < 60) return sign + Loc.T("Common.MinutesFormat", minutes);
        int h = minutes / 60, m = minutes % 60;
        return sign + (m == 0 ? Loc.T("Common.HoursFormat", h) : Loc.T("Common.HoursMinutesFormat", h, m));
    }

    public static string Date(DateOnly date, CultureInfo culture, string formatKey = "Note.ShortDateFormat")
        => date.ToString(Loc.T(formatKey) is var f && f != formatKey ? f : "d MMM", culture);

    /// <summary>One line from the <c>Note.Nudges</c> array; changes every hour.</summary>
    public static string Nudge(DateTime localNow)
    {
        if (Application.Current?.TryFindResource("Note.Nudges") is not Array { Length: > 0 } nudges) return "";
        int index = (localNow.DayOfYear * 24 + localNow.Hour) % nudges.Length;
        return nudges.GetValue(index) as string ?? "";
    }
}
