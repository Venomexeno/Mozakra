using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>Add or edit a plan (course or work), with validation and a live schedule preview.</summary>
public sealed partial class PlanEditorViewModel : ObservableObject
{
    private static readonly HashSet<string> OutputProperties =
        [nameof(PreviewText), nameof(PreviewIsWarning), nameof(ErrorsText), nameof(HasErrors), nameof(Title)];

    private readonly AppStateService _state;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly LocalizationService _localization;
    private readonly DispatcherTimer _previewTimer;
    private StudyPlan? _original;
    private bool _showErrors;
    private bool _loading;

    public PlanEditorViewModel(AppStateService state, IScheduler scheduler, IClock clock, LocalizationService localization)
    {
        _state = state;
        _scheduler = scheduler;
        _clock = clock;
        _localization = localization;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); UpdatePreview(); };

        var palette = Application.Current?.TryFindResource("PlanPalette") as string[] ?? ["#FFF3A3"];
        foreach (var hex in palette) Colors.Add(new PlanEditorColor(hex));
        Load(null);
    }

    /// <summary>Raised after a successful save.</summary>
    public event EventHandler? Saved;

    public Guid PlanId { get; private set; } = Guid.NewGuid();
    public bool IsEditing => _original is not null;
    public string Title => Loc.T(IsEditing ? "Screens.Editor.EditTitle" : "Screens.Editor.NewTitle");

    public ObservableCollection<PlanEditorColor> Colors { get; } = [];
    public ObservableCollection<PlanEditorOption> StartsAfterOptions { get; } = [];
    public ObservableCollection<PlanEditorChecklistItem> Checklist { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsWork), nameof(ShowAdjustRemaining))] private bool _isCourse = true;
    public bool IsWork { get => !IsCourse; set => IsCourse = !value; }
    public bool ShowAdjustRemaining => IsWork && IsEditing;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _videosText = "";
    [ObservableProperty] private string _hoursText = "";
    [ObservableProperty] private string _minutesText = "";
    [ObservableProperty] private string _videosDoneText = "0";
    [ObservableProperty] private string _estimatedHoursText = "";
    [ObservableProperty] private string _adjustRemainingText = "";
    [ObservableProperty] private string _currentRemainingText = "";
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _deadline;
    [ObservableProperty] private bool _noDeadline;
    [ObservableProperty] private PlanEditorOption? _selectedStartsAfter;
    [ObservableProperty] private string _selectedColorHex = "#FFF3A3";
    [ObservableProperty] private string _newChecklistTitle = "";

    [ObservableProperty] private string _previewText = "";
    [ObservableProperty] private bool _previewIsWarning;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasErrors))] private string _errorsText = "";
    public bool HasErrors => ErrorsText.Length > 0;

    /// <summary>Fills the form from <paramref name="planId"/>, or with defaults for a new plan.</summary>
    public void Load(Guid? planId)
    {
        _loading = true;
        var state = _state.State;
        var today = _clock.Today.ToDateTime(TimeOnly.MinValue);
        _original = planId is { } id ? state.Plans.FirstOrDefault(p => p.Id == id) : null;
        var plan = _original ?? new StudyPlan { ColorHex = NextFreeColor(state) };
        PlanId = plan.Id;

        IsCourse = plan.Kind == PlanKind.Course;
        Name = plan.Name;
        VideosText = plan.TotalVideos > 0 ? plan.TotalVideos.ToString() : "";
        HoursText = plan.TotalVideoMinutes > 0 ? (plan.TotalVideoMinutes / 60).ToString() : "";
        MinutesText = plan.TotalVideoMinutes > 0 ? (plan.TotalVideoMinutes % 60).ToString() : "";
        VideosDoneText = plan.VideosDone.ToString();
        EstimatedHoursText = plan.EstimatedMinutes > 0 ? PlanEditorInput.Format(plan.EstimatedMinutes / 60.0) : "";
        AdjustRemainingText = "";
        CurrentRemainingText = _original is null ? "" : Loc.T("Screens.Editor.CurrentRemaining",
            PlanEditorInput.Duration(RemainingCalculator.Remaining(plan, state.Logs.Where(l => l.PlanId == plan.Id))));
        StartDate = _original is null ? today : plan.StartDate.ToDateTime(TimeOnly.MinValue);
        NoDeadline = _original is not null && plan.Deadline is null;
        Deadline = plan.Deadline?.ToDateTime(TimeOnly.MinValue) ?? today.AddMonths(1);
        SelectedColorHex = plan.ColorHex;
        UpdateColorSelection();
        Checklist.Clear();
        foreach (var item in plan.Checklist) Checklist.Add(new PlanEditorChecklistItem { Id = item.Id, Title = item.Title, Done = item.Done });
        FillStartsAfter(state, plan);
        _showErrors = false;
        ErrorsText = "";
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(ShowAdjustRemaining));
        OnPropertyChanged(nameof(Title));
        _loading = false;
        UpdatePreview();
    }

    /// <summary>Validates and saves. Returns false (and shows the errors) when the form is invalid.</summary>
    public bool TrySave()
    {
        var (draft, errors) = PlanEditorValidator.Build(this, _original, _clock.Today);
        if (draft is null)
        {
            _showErrors = true;
            ErrorsText = string.Join(Environment.NewLine, errors.Select(e => "• " + e));
            return false;
        }

        var plans = _state.State.Plans;
        int index = plans.FindIndex(p => p.Id == draft.Id);
        if (index >= 0) plans[index] = draft;
        else plans.Add(draft);
        _state.Save();
        Load(draft.Id);
        Saved?.Invoke(this, EventArgs.Empty);
        return true;
    }

    [RelayCommand]
    private void Save() => TrySave();

    [RelayCommand]
    private void SelectColor(PlanEditorColor? color)
    {
        if (color is not null) SelectedColorHex = color.Hex;
    }

    [RelayCommand]
    private void AddChecklistItem()
    {
        if (string.IsNullOrWhiteSpace(NewChecklistTitle)) return;
        Checklist.Add(new PlanEditorChecklistItem { Title = NewChecklistTitle.Trim() });
        NewChecklistTitle = "";
    }

    [RelayCommand]
    private void RemoveChecklistItem(PlanEditorChecklistItem? item)
    {
        if (item is not null) Checklist.Remove(item);
    }

    [RelayCommand]
    private void MoveChecklistItemUp(PlanEditorChecklistItem? item) => Move(item, -1);

    [RelayCommand]
    private void MoveChecklistItemDown(PlanEditorChecklistItem? item) => Move(item, +1);

    partial void OnSelectedColorHexChanged(string value) => UpdateColorSelection();

    private void UpdateColorSelection()
    {
        foreach (var color in Colors)
            color.IsSelected = string.Equals(color.Hex, SelectedColorHex, StringComparison.OrdinalIgnoreCase);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading || e.PropertyName is null || OutputProperties.Contains(e.PropertyName)) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void UpdatePreview()
    {
        var (draft, errors) = PlanEditorValidator.Build(this, _original, _clock.Today);
        ErrorsText = _showErrors ? string.Join(Environment.NewLine, errors.Select(e => "• " + e)) : "";
        if (draft is null)
        {
            PreviewText = Loc.T("Screens.Editor.Preview.Incomplete");
            PreviewIsWarning = false;
            return;
        }
        var result = PlanEditorPreview.Compute(_state.State, draft, _scheduler, _clock.Today, _localization.Culture);
        PreviewText = result.Text;
        PreviewIsWarning = result.IsWarning;
    }

    private void Move(PlanEditorChecklistItem? item, int delta)
    {
        if (item is null) return;
        int from = Checklist.IndexOf(item), to = from + delta;
        if (from < 0 || to < 0 || to >= Checklist.Count) return;
        Checklist.Move(from, to);
    }

    /// <summary>Other active plans, minus any that (directly or not) start after this one: no cycles.</summary>
    private void FillStartsAfter(AppState state, StudyPlan plan)
    {
        StartsAfterOptions.Clear();
        var none = new PlanEditorOption(null, Loc.T("Screens.Editor.StartsAfterNone"));
        StartsAfterOptions.Add(none);
        foreach (var other in state.Plans)
        {
            if (other.Id == plan.Id || (other.IsArchived && other.Id != plan.StartsAfterPlanId)) continue;
            if (DependsOn(state, other, plan.Id)) continue;
            StartsAfterOptions.Add(new PlanEditorOption(other.Id, other.Name));
        }
        SelectedStartsAfter = StartsAfterOptions.FirstOrDefault(o => o.Id == plan.StartsAfterPlanId) ?? none;
    }

    private static bool DependsOn(AppState state, StudyPlan candidate, Guid planId)
    {
        var seen = new HashSet<Guid>();
        for (var current = candidate; current?.StartsAfterPlanId is { } next; current = state.Plans.FirstOrDefault(p => p.Id == next))
        {
            if (next == planId) return true;
            if (!seen.Add(next)) return false;
        }
        return false;
    }

    private string NextFreeColor(AppState state)
    {
        var used = state.Plans.Where(p => !p.IsArchived).Select(p => p.ColorHex).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Colors.FirstOrDefault(c => !used.Contains(c.Hex))?.Hex ?? Colors.FirstOrDefault()?.Hex ?? "#FFF3A3";
    }
}
