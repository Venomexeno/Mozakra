using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.Core.Tests.Persistence;

public class JsonStateStoreTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly TestClock _clock = new(new DateOnly(2026, 11, 20));

    public void Dispose() => _dir.Dispose();

    private JsonStateStore NewStore() => new(_dir.Path, _clock);

    private static AppState SampleState()
    {
        var plan = new StudyPlan
        {
            Name = "LINQ",
            Kind = PlanKind.Work,
            StartDate = new DateOnly(2026, 11, 1),
            Deadline = new DateOnly(2027, 1, 15),
            EstimatedMinutes = 600,
            RemainingOverrideMinutes = 300,
            RemainingOverrideSetOn = new DateOnly(2026, 11, 10),
            Checklist = { new ChecklistItem { Title = "Read docs", Done = true } },
        };
        var state = new AppState
        {
            Plans = { plan },
            Logs = { new LogEntry { PlanId = plan.Id, Date = new DateOnly(2026, 11, 19), Minutes = 25, VideosDone = 2 } },
            Exceptions = { new AvailabilityException { From = new DateOnly(2026, 12, 1), To = new DateOnly(2026, 12, 3), Label = "Trip" } },
            Timer = new ActiveTimer { PlanId = plan.Id, AccumulatedMs = 1234.5 },
            Settings = new AppSettings { Language = "en", NoteLeft = 10.5, ShowDetails = true },
        };
        state.Weekly.MinutesByDay[2] = 120;
        state.Weekly.ReminderByDay[2] = new TimeOnly(19, 30);
        return state;
    }

    [Fact]
    public void Load_WithNoFile_ReturnsNewState()
    {
        var state = NewStore().Load();

        Assert.Empty(state.Plans);
        Assert.Equal(1, state.SchemaVersion);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var original = SampleState();
        var store = NewStore();

        store.Save(original);
        var loaded = NewStore().Load();

        Assert.Equal(StateJson.Serialize(original), StateJson.Serialize(loaded));
        var plan = Assert.Single(loaded.Plans);
        Assert.Equal(new DateOnly(2027, 1, 15), plan.Deadline);
        Assert.Equal(PlanKind.Work, plan.Kind);
        Assert.Equal(new TimeOnly(19, 30), loaded.Weekly.ReminderByDay[2]);
        Assert.Equal(new DateOnly(2026, 11, 19), loaded.Logs[0].Date);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = NewStore();

        store.Save(SampleState());
        store.Save(SampleState());

        Assert.True(File.Exists(store.StatePath));
        Assert.False(File.Exists(store.StatePath + ".tmp"));
    }

    [Fact]
    public void Load_CorruptFile_RecoversFromNewestBackupAndKeepsCorruptCopy()
    {
        var store = NewStore();
        var state = SampleState();
        store.Save(state);                                     // creates today's backup
        File.WriteAllText(store.StatePath, "{ this is not json");

        var loaded = store.Load();

        Assert.Equal("LINQ", Assert.Single(loaded.Plans).Name);
        Assert.NotNull(store.LastCorruptFilePath);
        Assert.True(File.Exists(store.LastCorruptFilePath));
        Assert.StartsWith("state.corrupt-", Path.GetFileName(store.LastCorruptFilePath));
        Assert.False(File.Exists(store.StatePath));
    }

    [Fact]
    public void Load_CorruptFileWithoutBackup_ReturnsNewState()
    {
        var store = NewStore();
        File.WriteAllText(store.StatePath, "null");

        var loaded = store.Load();

        Assert.Empty(loaded.Plans);
        Assert.NotNull(store.LastCorruptFilePath);
    }

    [Fact]
    public void Load_NewerSchemaVersion_Throws()
    {
        var store = NewStore();
        File.WriteAllText(store.StatePath, """{ "SchemaVersion": 99 }""");

        var ex = Assert.Throws<UnsupportedSchemaVersionException>(store.Load);

        Assert.Equal(99, ex.FoundVersion);
        Assert.True(File.Exists(store.StatePath));            // not treated as corrupt
    }

    [Fact]
    public void Load_NullCollections_AreNormalized()
    {
        var store = NewStore();
        File.WriteAllText(store.StatePath, """{ "SchemaVersion": 1, "Plans": null, "Weekly": { "MinutesByDay": [10, 20] } }""");

        var loaded = store.Load();

        Assert.Empty(loaded.Plans);
        Assert.Equal(7, loaded.Weekly.MinutesByDay.Length);
        Assert.Equal(20, loaded.Weekly.MinutesByDay[1]);
    }
}
