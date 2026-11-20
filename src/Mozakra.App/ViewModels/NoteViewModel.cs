using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>
/// The sticky note: header, today's agenda, toast, undo and navigation.
/// Logging (time + videos finished, by hand), warnings and details live in the other NoteViewModel.*.cs partial files.
/// Every change goes through <see cref="Mutate"/> (undo snapshot → change → save → refresh).
/// </summary>
public sealed partial class NoteViewModel : ObservableObject
{
    private readonly AppStateService _state;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly INavigationService _navigation;
    private readonly INoteDialogs _dialogs;
    private readonly LocalizationService _localization;
    private readonly NoteUndoStack _undo = new(30);
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private (DateOnly Day, int Hour) _lastRefresh;

    public NoteViewModel(AppStateService state, IScheduler scheduler, IClock clock,
        INavigationService navigation, INoteDialogs dialogs, LocalizationService localization)
    {
        _state = state;
        _scheduler = scheduler;
        _clock = clock;
        _navigation = navigation;
        _dialogs = dialogs;
        _localization = localization;
        _logDate = clock.Today.ToDateTime(TimeOnly.MinValue);

        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); IsToastVisible = false; };
        _clockTimer.Tick += (_, _) => { if (_lastRefresh != (Today, DateTime.Now.Hour)) Refresh(); };
        _clockTimer.Start();
        _state.Changed += (_, _) => Refresh();
        _localization.LanguageChanged += (_, _) => Refresh();

        Refresh();
        TakeOverOldTimer();
    }

    private AppState State => _state.State;
    private DateOnly Today => _clock.Today;
    private CultureInfo Culture => _localization.Culture;

    // ----- Header -----
    [ObservableProperty] private string _dateText = "";
    [ObservableProperty] private string _nudgeText = "";

    // ----- Agenda -----
    public ObservableCollection<NoteAgendaItem> Agenda { get; } = [];
    public ObservableCollection<NotePlanOption> PlanOptions { get; } = [];

    [ObservableProperty] private bool _hasNoPlans;
    [ObservableProperty] private bool _isRestDay;
    [ObservableProperty] private bool _isGoalReached;
    [ObservableProperty] private bool _isAllComplete;
    [ObservableProperty] private string _todaySummary = "";
    [ObservableProperty] private double _todayPercent;

    // ----- Toast + undo -----
    [ObservableProperty] private string _toastText = "";
    [ObservableProperty] private bool _isToastVisible;

    public bool CanUndo => _undo.Count > 0;
    public string UndoTooltip => _undo.NextLabel is { } label ? Loc.T("Note.UndoTooltip", label) : "";

    /// <summary>Recomputes everything from the shared state (also runs after every save, hourly and on language change).</summary>
    public void Refresh()
    {
        var state = State;
        var today = Today;
        _lastRefresh = (today, DateTime.Now.Hour);

        var active = state.Plans.Where(p => !p.IsArchived).ToList();
        var day = _scheduler.GetDay(state, today);
        var statuses = _scheduler.GetStatuses(state).ToDictionary(s => s.PlanId);

        DateText = NoteFormat.Date(today, Culture, "Note.DateFormat");
        NudgeText = NoteFormat.Nudge(DateTime.Now);

        RefreshAgenda(day, statuses, active);
        RefreshPlanOptions(day, statuses, active);
        RefreshWarnings(statuses, active);
        RefreshDetails(statuses, active);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoTooltip));
    }

    private void RefreshAgenda(DailyPlan day, Dictionary<Guid, PlanStatus> statuses, List<StudyPlan> active)
    {
        Agenda.Clear();
        foreach (var target in day.Targets)
        {
            var plan = active.FirstOrDefault(p => p.Id == target.PlanId);
            if (plan is null || (target.TargetMinutes == 0 && target.LoggedMinutes <= 0)) continue;
            var health = statuses.TryGetValue(plan.Id, out var s) ? s.Health : PlanHealth.NoDeadline;
            int videosToday = State.Logs.Where(l => l.PlanId == plan.Id && l.Date == Today).Sum(l => l.VideosDone);
            Agenda.Add(BuildAgendaItem(plan, target, health, videosToday));
        }

        HasNoPlans = active.Count == 0;
        IsRestDay = !HasNoPlans && day.CapacityMinutes == 0;
        IsAllComplete = !HasNoPlans && !IsRestDay && statuses.Values.All(s => s.Health == PlanHealth.Completed);
        IsGoalReached = !HasNoPlans && day.TotalTargetMinutes > 0 && day.LoggedMinutes >= day.TotalTargetMinutes;

        TodayPercent = day.TotalTargetMinutes > 0 ? Math.Min(100, 100.0 * day.LoggedMinutes / day.TotalTargetMinutes) : 0;
        TodaySummary = day.TotalTargetMinutes > 0
            ? Loc.T("Note.TodaySummary", NoteFormat.Duration(day.LoggedMinutes), NoteFormat.Duration(day.TotalTargetMinutes))
            : Loc.T("Note.TodayLogged", NoteFormat.Duration(Math.Max(0, day.LoggedMinutes)));
    }

    private static NoteAgendaItem BuildAgendaItem(StudyPlan plan, PlanTarget target, PlanHealth health, int videosToday)
    {
        int logged = Math.Max(0, target.LoggedMinutes);
        string targetText, progress;
        double percent;

        if (plan.Kind == PlanKind.Course)
        {
            // Courses are planned and logged in videos; the minutes are only a hint.
            int videos = target.TargetVideos ?? 0;
            videosToday = Math.Max(0, videosToday);
            targetText = target.TargetMinutes > 0 && videos > 0
                ? $"{Loc.T("Common.VideosFormat", videos)} · ≈ {NoteFormat.Duration(target.TargetMinutes)}"
                : Loc.T("Note.Bonus");
            progress = videos > 0 ? Loc.T("Note.VideosProgress", videosToday, videos) : Loc.T("Common.VideosFormat", videosToday);
            percent = videos > 0 ? Math.Min(100, 100.0 * videosToday / videos) : 100;
        }
        else
        {
            targetText = target.TargetMinutes > 0 ? NoteFormat.Duration(target.TargetMinutes) : Loc.T("Note.Bonus");
            progress = target.TargetMinutes > 0
                ? $"{NoteFormat.Duration(logged)} / {NoteFormat.Duration(target.TargetMinutes)}"
                : NoteFormat.Duration(logged);
            percent = target.TargetMinutes > 0 ? Math.Min(100, 100.0 * logged / target.TargetMinutes) : 100;
        }

        return new NoteAgendaItem(plan.Id, plan.Name, plan.ColorHex, targetText, progress, percent,
            NoteHealthText.Icon(health), NoteHealthText.Label(health), percent >= 100);
    }

    private void RefreshPlanOptions(DailyPlan day, Dictionary<Guid, PlanStatus> statuses, List<StudyPlan> active)
    {
        var targetOf = day.Targets.ToDictionary(t => t.PlanId, t => t.TargetMinutes);
        var options = active
            .Where(p => !statuses.TryGetValue(p.Id, out var s) || s.Health != PlanHealth.Completed)
            .OrderByDescending(p => p.Id == day.MostUrgentPlanId)
            .ThenByDescending(p => targetOf.GetValueOrDefault(p.Id))
            .ThenBy(p => p.Name, StringComparer.CurrentCulture)
            .Select(p => new NotePlanOption(p.Id, p.Name, p.ColorHex, p.Kind == PlanKind.Course))
            .ToList();

        var keepLog = LogPlan?.Id;
        PlanOptions.Clear();
        foreach (var option in options) PlanOptions.Add(option);

        // Keep the plan the user picked; otherwise suggest the most urgent one.
        LogPlan = options.FirstOrDefault(o => o.Id == keepLog)
               ?? options.FirstOrDefault(o => o.Id == day.MostUrgentPlanId)
               ?? options.FirstOrDefault();
    }

    /// <summary>Snapshot for undo, apply the change, save (which refreshes the note).</summary>
    private void Mutate(string undoLabel, Action<AppState> change)
    {
        _undo.Push(undoLabel, State);
        change(State);
        _state.Save();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undo.Pop() is not { } item)
        {
            ShowToast(Loc.T("Note.Toast.NothingToUndo"));
            return;
        }
        item.State.Settings = State.Settings;   // undo never moves the note or flips the language
        _state.Replace(item.State);
        ShowToast(Loc.T("Note.Toast.Undone", item.Label));
    }

    public void ShowToast(string message)
    {
        _toastTimer.Stop();
        ToastText = message;
        IsToastVisible = message.Length > 0;
        if (IsToastVisible) _toastTimer.Start();
    }

    [RelayCommand] private void HideToast() => ShowToast("");

    // ----- Window position (drag anywhere on the card) -----
    public (double Left, double Top)? SavedPosition =>
        State.Settings is { NoteLeft: { } left, NoteTop: { } top } ? (left, top) : null;

    public void SavePosition(double left, double top)
    {
        if (double.IsNaN(left) || double.IsNaN(top)) return;
        State.Settings.NoteLeft = Math.Round(left);
        State.Settings.NoteTop = Math.Round(top);
        _state.Save();
    }

    // ----- Navigation -----
    [RelayCommand] private void Hide() => _navigation.HideNote();
    [RelayCommand] private void OpenPlans() => _navigation.OpenPlans();
    [RelayCommand] private void OpenFreeTime() => _navigation.OpenFreeTime();
    [RelayCommand] private void OpenSettings() => _navigation.OpenSettings();
    [RelayCommand] private void AddFirstPlan() => _navigation.OpenPlanEditor(null);
}
