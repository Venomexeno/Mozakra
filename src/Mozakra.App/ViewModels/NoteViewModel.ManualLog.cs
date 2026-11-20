using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>
/// Logging by hand. A course is logged in videos finished (its time is worked out from the course's average
/// minutes per video, so the daily targets keep working); a work plan is logged in minutes. Quick chips add to the
/// box; nothing is saved until "Log" is pressed.
/// </summary>
public sealed partial class NoteViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLogPlanCourse), nameof(IsLogPlanWork))]
    private NotePlanOption? _logPlan;

    [ObservableProperty] private DateTime _logDate;
    [ObservableProperty] private string _logMinutesText = "";
    [ObservableProperty] private string _logVideosText = "";

    public bool IsLogPlanCourse => LogPlan?.IsCourse == true;
    public bool IsLogPlanWork => LogPlan is { IsCourse: false };
    public DateTime MaxLogDate => Today.ToDateTime(TimeOnly.MinValue);

    [RelayCommand]
    private void QuickAdd(string? minutes)
    {
        if (!int.TryParse(minutes, out int add)) return;
        int current = int.TryParse(LogMinutesText?.Trim(), out int m) && m > 0 ? m : 0;
        LogMinutesText = Math.Min(current + add, 24 * 60).ToString();
    }

    [RelayCommand]
    private void QuickAddVideos(string? videos)
    {
        if (!int.TryParse(videos, out int add)) return;
        int current = int.TryParse(LogVideosText?.Trim(), out int v) && v > 0 ? v : 0;
        LogVideosText = (current + add).ToString();
    }

    [RelayCommand]
    private void AddLog()
    {
        if (!TryGetLogTarget(out var option, out var date)) return;
        var plan = State.Plans.FirstOrDefault(p => p.Id == option.Id);
        if (plan is null) return;

        if (plan.Kind == PlanKind.Course)
        {
            int left = Math.Max(0, plan.TotalVideos - plan.VideosDone);
            if (left == 0) { ShowToast(Loc.T("Note.Toast.AllVideosDone")); return; }
            if (!TryReadNumber(LogVideosText, left, out int videos) || videos == 0)
            {
                ShowToast(Loc.T(string.IsNullOrWhiteSpace(LogVideosText) ? "Note.Toast.EnterVideos" : "Note.Toast.VideosRange", left));
                return;
            }
            LogVideosText = "";
            ShowToast(LogAndDescribe(Loc.T("Note.Undo.Videos", $"+{videos}"), plan.Id, date, VideoMinutes(plan, videos), videos));
            return;
        }

        if (!TryReadNumber(LogMinutesText, 24 * 60, out int minutes) || minutes == 0)
        {
            ShowToast(Loc.T("Note.Toast.EnterMinutes"));
            return;
        }
        LogMinutesText = "";
        ShowToast(LogAndDescribe(Loc.T("Note.Undo.Add", NoteFormat.Duration(minutes)), plan.Id, date, minutes, 0));
    }

    /// <summary>Corrections: removes videos (courses) or minutes (work) logged on the chosen day, never more than was logged.</summary>
    [RelayCommand]
    private void SubtractLog()
    {
        if (!TryGetLogTarget(out var option, out var date)) return;
        var plan = State.Plans.FirstOrDefault(p => p.Id == option.Id);
        if (plan is null) return;
        var dayLogs = State.Logs.Where(l => l.PlanId == plan.Id && l.Date == date).ToList();

        if (plan.Kind == PlanKind.Course)
        {
            if (!TryReadNumber(LogVideosText, plan.TotalVideos, out int v) || v == 0) { ShowToast(Loc.T("Note.Toast.EnterVideos")); return; }
            int loggedVideos = dayLogs.Sum(l => l.VideosDone);
            if (loggedVideos <= 0) { ShowToast(Loc.T("Note.Toast.NothingToSubtract")); return; }
            int removeVideos = Math.Min(v, loggedVideos);
            int removeMinutes = Math.Min(VideoMinutes(plan, removeVideos), Math.Max(0, dayLogs.Sum(l => l.Minutes)));
            LogVideosText = "";
            ShowToast(LogAndDescribe(Loc.T("Note.Undo.Videos", $"-{removeVideos}"), plan.Id, date, -removeMinutes, -removeVideos));
            return;
        }

        if (!TryReadNumber(LogMinutesText, 24 * 60, out int m) || m == 0) { ShowToast(Loc.T("Note.Toast.EnterMinutes")); return; }
        int logged = dayLogs.Sum(l => l.Minutes);
        if (logged <= 0) { ShowToast(Loc.T("Note.Toast.NothingToSubtract")); return; }
        int remove = Math.Min(m, logged);
        LogMinutesText = "";
        ShowToast(LogAndDescribe(Loc.T("Note.Undo.Subtract", NoteFormat.Duration(remove)), plan.Id, date, -remove, 0));
    }

    /// <summary>Time a number of videos stands for: the course's average minutes per video.</summary>
    private static int VideoMinutes(StudyPlan plan, int videos) =>
        (int)Math.Round(videos * RemainingCalculator.MinutesPerVideo(plan), MidpointRounding.AwayFromZero);

    /// <summary>Empty = 0. Otherwise a whole number from 0 to <paramref name="max"/>.</summary>
    private static bool TryReadNumber(string? text, int max, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return true;
        return int.TryParse(text.Trim(), out value) && value >= 0 && value <= max;
    }

    private bool TryGetLogTarget(out NotePlanOption plan, out DateOnly date)
    {
        plan = LogPlan!;
        date = DateOnly.FromDateTime(LogDate);
        if (LogPlan is null)
        {
            ShowToast(Loc.T("Note.Toast.PickPlan"));
            return false;
        }
        if (date > Today)
        {
            ShowToast(Loc.T("Note.Toast.FutureDay"));
            return false;
        }
        return true;
    }

    /// <summary>
    /// Earlier builds had a study timer. If one was left in the saved state, put its minutes in the log box
    /// (for its plan) so they aren't lost, and drop the timer. For a course, the minutes become a video estimate.
    /// </summary>
    private void TakeOverOldTimer()
    {
        if (State.Timer is not { } timer) return;
        double ms = timer.AccumulatedMs;
        if (timer.Running) ms += Math.Max(0, (_clock.UtcNow - timer.StartedAtUtc).TotalMilliseconds);
        int minutes = (int)Math.Round(TimeSpan.FromMilliseconds(ms).TotalMinutes, MidpointRounding.AwayFromZero);

        State.Timer = null;
        _state.Save();

        if (minutes < 1 || PlanOptions.FirstOrDefault(o => o.Id == timer.PlanId) is not { } option) return;
        LogPlan = option;
        if (option.IsCourse && State.Plans.FirstOrDefault(p => p.Id == option.Id) is { } plan
            && RemainingCalculator.MinutesPerVideo(plan) > 0)
            LogVideosText = Math.Max(1, (int)Math.Round(minutes / RemainingCalculator.MinutesPerVideo(plan))).ToString();
        else
            LogMinutesText = Math.Min(minutes, 24 * 60).ToString();
        ShowToast(Loc.T("Note.Toast.OldTimer", NoteFormat.Duration(minutes)));
    }

    /// <summary>Adds a log entry (and the videos to the course), saves with undo, and returns the toast text.</summary>
    private string LogAndDescribe(string undoLabel, Guid planId, DateOnly date, int minutes, int videos)
    {
        var tomorrow = Today.AddDays(1);
        int tomorrowBefore = _scheduler.GetDay(State, tomorrow).TotalTargetMinutes;
        int appliedVideos = 0;
        bool isCourse = false;

        Mutate(undoLabel, s =>
        {
            var plan = s.Plans.FirstOrDefault(p => p.Id == planId);
            isCourse = plan is { Kind: PlanKind.Course };
            if (isCourse && videos != 0)
            {
                int done = Math.Clamp(plan!.VideosDone + videos, 0, Math.Max(0, plan.TotalVideos));
                appliedVideos = done - plan.VideosDone;
                plan.VideosDone = done;
            }
            s.Logs.Add(new LogEntry
            {
                PlanId = planId,
                Date = date,
                Minutes = minutes,
                VideosDone = appliedVideos,
                CreatedAtUtc = _clock.UtcNow,
            });
        });

        int tomorrowAfter = _scheduler.GetDay(State, tomorrow).TotalTargetMinutes;
        var message = NoteToastBuilder.AfterLog(isCourse ? 0 : minutes, appliedVideos, tomorrowBefore, tomorrowAfter,
            _scheduler.GetDay(State, Today));
        var finished = State.Plans.FirstOrDefault(p => p.Id == planId);
        if (finished is { Kind: PlanKind.Course } && appliedVideos > 0 && finished.VideosDone >= finished.TotalVideos)
            message += "\n" + Loc.T("Note.Toast.CourseDone", finished.Name);
        return message;
    }
}
