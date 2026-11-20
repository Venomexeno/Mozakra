using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.App.ViewModels;
using Mozakra.Core.Models;
using Mozakra.Core.Persistence;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.Tests;

/// <summary>
/// Builds a NoteViewModel over an in-memory state, a fixed clock and fake dialogs/navigation.
/// Without a WPF Application, Loc.T returns the key, so assertions use keys rather than translated text.
/// </summary>
internal sealed class NoteTestBed
{
    public static readonly DateOnly Today = new(2026, 11, 18); // Wednesday

    public FakeClock Clock { get; } = new(Today);
    public InMemoryStore Store { get; }
    public AppStateService State { get; }
    public FakeDialogs Dialogs { get; } = new();
    public FakeNavigation Navigation { get; } = new();
    public Scheduler Scheduler { get; }

    public NoteTestBed(AppState state)
    {
        Store = new InMemoryStore(state);
        State = new AppStateService(Store);
        Scheduler = new Scheduler(Clock);
    }

    public NoteViewModel CreateViewModel() =>
        new(State, Scheduler, Clock, Navigation, Dialogs, new LocalizationService());

    /// <summary>60 free minutes every day; a 10-video course (10 min/video, deadline in 9 days) and an open work plan.</summary>
    public static AppState SampleState(out StudyPlan course, out StudyPlan work)
    {
        course = new StudyPlan
        {
            Name = "LINQ", Kind = PlanKind.Course, StartDate = Today.AddDays(-5), Deadline = Today.AddDays(9),
            TotalVideos = 10, TotalVideoMinutes = 100,
        };
        work = new StudyPlan { Name = "Side project", Kind = PlanKind.Work, StartDate = Today.AddDays(-5), EstimatedMinutes = 600 };
        var state = new AppState { Plans = { course, work } };
        Array.Fill(state.Weekly.MinutesByDay, 60);
        return state;
    }
}

internal sealed class FakeClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;
    public DateTime UtcNow { get; set; } = today.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);
}

internal sealed class InMemoryStore(AppState state) : IStateStore
{
    public string DataDirectory => "memory";
    public int Saves { get; private set; }
    public AppState Load() => state;
    public void Save(AppState s) => Saves++;
}

internal sealed class FakeDialogs : INoteDialogs
{
    public bool ConfirmAnswer { get; set; } = true;
    public string? LastConfirm { get; private set; }
    public Func<IReadOnlyList<NoteEditDayRow>, IReadOnlyList<NoteEditDayRow>?>? EditDayAnswer { get; set; }

    public bool Confirm(string message)
    {
        LastConfirm = message;
        return ConfirmAnswer;
    }

    public IReadOnlyList<NoteEditDayRow>? EditDay(string title, IReadOnlyList<NoteEditDayRow> rows)
        => EditDayAnswer?.Invoke(rows);
}

internal sealed class FakeNavigation : INavigationService
{
    public List<string> Calls { get; } = [];
    public void ShowNote() => Calls.Add(nameof(ShowNote));
    public void HideNote() => Calls.Add(nameof(HideNote));
    public void OpenPlans() => Calls.Add(nameof(OpenPlans));
    public void OpenPlanEditor(Guid? planId) => Calls.Add(nameof(OpenPlanEditor));
    public void OpenFreeTime() => Calls.Add(nameof(OpenFreeTime));
    public void OpenSettings() => Calls.Add(nameof(OpenSettings));
    public void OpenOnboarding() => Calls.Add(nameof(OpenOnboarding));
}
