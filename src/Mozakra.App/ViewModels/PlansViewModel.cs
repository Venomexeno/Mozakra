using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>The plans window: active plans (with health) and archived ones; add, edit, archive, delete.</summary>
public sealed partial class PlansViewModel : ObservableObject, IDisposable
{
    private readonly AppStateService _state;
    private readonly IScheduler _scheduler;
    private readonly INavigationService _navigation;
    private readonly LocalizationService _localization;

    public PlansViewModel(AppStateService state, IScheduler scheduler, INavigationService navigation,
        LocalizationService localization)
    {
        _state = state;
        _scheduler = scheduler;
        _navigation = navigation;
        _localization = localization;
        _state.Changed += OnChanged;
        _localization.LanguageChanged += OnChanged;
        Refresh();
    }

    public ObservableCollection<PlansItemViewModel> Active { get; } = [];
    public ObservableCollection<PlansItemViewModel> Archived { get; } = [];

    [ObservableProperty] private bool _hasActive;
    [ObservableProperty] private bool _hasArchived;
    [ObservableProperty] private bool _isEmpty;

    public void Refresh()
    {
        var state = _state.State;
        var culture = _localization.Culture;
        var statuses = _scheduler.GetStatuses(state).ToDictionary(s => s.PlanId);

        Active.Clear();
        Archived.Clear();
        foreach (var plan in state.Plans)
        {
            if (plan.IsArchived)
            {
                var logs = state.Logs.Where(l => l.PlanId == plan.Id);
                Archived.Add(new PlansItemViewModel(plan, RemainingCalculator.ProgressPercent(plan, logs), null, culture));
            }
            else if (statuses.TryGetValue(plan.Id, out var status))
            {
                Active.Add(new PlansItemViewModel(plan, status.ProgressPercent, status.Health, culture));
            }
        }
        HasActive = Active.Count > 0;
        HasArchived = Archived.Count > 0;
        IsEmpty = !HasActive;
    }

    [RelayCommand]
    private void Add() => _navigation.OpenPlanEditor(null);

    [RelayCommand]
    private void Edit(PlansItemViewModel? item)
    {
        if (item is not null) _navigation.OpenPlanEditor(item.Id);
    }

    [RelayCommand]
    private void Archive(PlansItemViewModel? item) => SetArchived(item, true);

    [RelayCommand]
    private void Unarchive(PlansItemViewModel? item) => SetArchived(item, false);

    [RelayCommand]
    private void Delete(PlansItemViewModel? item)
    {
        if (item is null) return;
        if (!PlansMessageBox.Confirm(Loc.T("Screens.Plans.DeleteConfirm", item.Name))) return;
        RemovePlan(_state.State, item.Id);
        _state.Save();
    }

    [RelayCommand]
    private void OpenFreeTime() => _navigation.OpenFreeTime();

    [RelayCommand]
    private void OpenSettings() => _navigation.OpenSettings();

    /// <summary>Removes the plan, its logs, and every reference to it (dependencies, a leftover timer).</summary>
    public static void RemovePlan(AppState state, Guid planId)
    {
        state.Plans.RemoveAll(p => p.Id == planId);
        state.Logs.RemoveAll(l => l.PlanId == planId);
        foreach (var plan in state.Plans.Where(p => p.StartsAfterPlanId == planId))
            plan.StartsAfterPlanId = null;
        if (state.Timer?.PlanId == planId) state.Timer = null;
    }

    private void SetArchived(PlansItemViewModel? item, bool archived)
    {
        if (item is null) return;
        var plan = _state.State.Plans.FirstOrDefault(p => p.Id == item.Id);
        if (plan is null) return;
        plan.IsArchived = archived;
        _state.Save();
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose()
    {
        _state.Changed -= OnChanged;
        _localization.LanguageChanged -= OnChanged;
    }
}
