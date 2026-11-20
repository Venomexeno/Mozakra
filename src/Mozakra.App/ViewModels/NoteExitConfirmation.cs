using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>
/// Handles the tray's Exit: asks with a motivational message ("You still have 35 min today 😅" /
/// "Done for today 🎉"), then saves and shuts down.
/// </summary>
public sealed class NoteExitConfirmation
{
    private readonly AppStateService _state;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly INoteDialogs _dialogs;
    private readonly Action _shutdown;

    public NoteExitConfirmation(AppStateService state, IScheduler scheduler, IClock clock, INoteDialogs dialogs,
        Action? shutdown = null)
    {
        _state = state;
        _scheduler = scheduler;
        _clock = clock;
        _dialogs = dialogs;
        _shutdown = shutdown ?? (() => System.Windows.Application.Current.Shutdown());
    }

    /// <summary>The question shown before exiting, based on what's left today.</summary>
    public string BuildMessage()
    {
        var day = _scheduler.GetDay(_state.State, _clock.Today);
        int left = day.TotalTargetMinutes - day.LoggedMinutes;
        if (day.TotalTargetMinutes > 0 && left > 0)
            return Loc.T("Note.Exit.Remaining", NoteFormat.Duration(left));
        if (day.TotalTargetMinutes > 0)
            return Loc.T("Note.Exit.Done");
        return Loc.T("Note.Exit.Plain");
    }

    /// <summary>Asks; on Yes saves and shuts down. Returns true when the app is exiting.</summary>
    public bool Run()
    {
        if (!_dialogs.Confirm(BuildMessage())) return false;
        _state.Save();
        _shutdown();
        return true;
    }
}
