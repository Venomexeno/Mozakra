using Mozakra.App.Localization;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>Builds the motivational message shown after a log ("+30 min 💪 · tomorrow is now 35 instead of 45").</summary>
public static class NoteToastBuilder
{
    public static string AfterLog(int minutes, int videos, int tomorrowBefore, int tomorrowAfter, DailyPlan today)
    {
        var lines = new List<string>();

        var first = minutes > 0 ? Loc.T("Note.Toast.Added", NoteFormat.Duration(minutes))
                  : minutes < 0 ? Loc.T("Note.Toast.Removed", NoteFormat.Duration(-minutes))
                  : "";
        if (videos != 0)
        {
            var v = Loc.T("Note.Toast.Videos", videos > 0 ? $"+{videos}" : videos.ToString());
            first = first.Length == 0 ? v : $"{first} · {v}";
        }
        if (first.Length > 0) lines.Add(first);

        if (tomorrowBefore != tomorrowAfter)
            lines.Add(Loc.T("Note.Toast.Tomorrow", NoteFormat.Duration(tomorrowAfter), NoteFormat.Duration(tomorrowBefore)));

        lines.Add(TodayLine(today));
        return string.Join("\n", lines.Where(l => l.Length > 0));
    }

    public static string TodayLine(DailyPlan today)
    {
        int target = today.TotalTargetMinutes, logged = today.LoggedMinutes;
        if (target <= 0) return logged > 0 ? Loc.T("Note.Toast.Bonus") : "";
        if (logged > target) return Loc.T("Note.Toast.Ahead", NoteFormat.Duration(logged - target));
        if (logged == target) return Loc.T("Note.Toast.GoalDone");
        return Loc.T("Note.Toast.Left", NoteFormat.Duration(target - logged));
    }
}
