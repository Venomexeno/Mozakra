using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.Core.Tests.Persistence;

public class LegacyStudyNoteImporterTests
{
    // The sample from docs/plans/02-persistence-and-import.md (with a real timestamp instead of "...").
    private const string SampleJson = """
        { "Start":"2026-11-20", "Logs":{"2026-11-20":25}, "Skips":["2026-11-21"], "Units":{"linq":18},
          "Checks":[], "Timer":{"StartedAtUtc":"2026-11-20T08:00:00Z","AccumulatedMs":4722741.5,"Running":false},
          "Topmost":false, "ShowDetails":true, "StartupConfigured":true, "Left":1439, "Top":4.85 }
        """;

    private static AppState ImportSample() =>
        new LegacyStudyNoteImporter(new TestClock(new DateOnly(2026, 11, 22))).Convert(SampleJson);

    [Fact]
    public void CreatesLinqAndEfCorePlans()
    {
        var state = ImportSample();

        Assert.Equal(2, state.Plans.Count);
        var linq = state.Plans[0];
        Assert.Equal("كورس LINQ", linq.Name);
        Assert.Equal(PlanKind.Course, linq.Kind);
        Assert.Equal(61, linq.TotalVideos);
        Assert.Equal(640, linq.TotalVideoMinutes);
        Assert.Equal(18, linq.VideosDone);
        Assert.Equal(new DateOnly(2026, 11, 20), linq.StartDate);
        Assert.Null(linq.Deadline);
        Assert.Null(linq.StartsAfterPlanId);

        var ef = state.Plans[1];
        Assert.Equal("كورس Entity Framework Core", ef.Name);
        Assert.Equal(PlanKind.Course, ef.Kind);
        Assert.Equal(70, ef.TotalVideos);
        Assert.Equal(456, ef.TotalVideoMinutes);
        Assert.Equal(0, ef.VideosDone);
        Assert.Equal(linq.Id, ef.StartsAfterPlanId);
        Assert.Null(ef.Deadline);
    }

    [Fact]
    public void SetsWeeklyAvailability()
    {
        Assert.Equal(new[] { 45, 45, 120, 45, 120, 180, 0 }, ImportSample().Weekly.MinutesByDay);
    }

    [Fact]
    public void ConvertsLogsSkipsAndTimer()
    {
        var state = ImportSample();
        var linqId = state.Plans[0].Id;

        var log = Assert.Single(state.Logs);
        Assert.Equal(linqId, log.PlanId);
        Assert.Equal(new DateOnly(2026, 11, 20), log.Date);
        Assert.Equal(25, log.Minutes);

        var skip = Assert.Single(state.Exceptions);
        Assert.Equal(new DateOnly(2026, 11, 21), skip.From);
        Assert.Equal(new DateOnly(2026, 11, 21), skip.To);
        Assert.Equal(0, skip.Minutes);
        Assert.Equal("سفر", skip.Label);

        Assert.NotNull(state.Timer);
        Assert.Equal(linqId, state.Timer.PlanId);
        Assert.False(state.Timer.Running);
        Assert.Equal(4722741.5, state.Timer.AccumulatedMs);
    }

    [Fact]
    public void ConvertsSettings()
    {
        var settings = ImportSample().Settings;

        Assert.Equal("ar", settings.Language);
        Assert.True(settings.OnboardingDone);
        Assert.Equal(1439, settings.NoteLeft);
        Assert.Equal(4.85, settings.NoteTop);
        Assert.True(settings.ShowDetails);
    }

    [Fact]
    public void ResultPassesImportValidationAndRoundTrips()
    {
        var state = ImportSample();

        Assert.Null(ExportImportService.FindMissingPlanReference(state));
        Assert.Equal(StateJson.Serialize(state), StateJson.Serialize(StateJson.Deserialize(StateJson.Serialize(state))));
    }

    [Fact]
    public void RunningTimer_IsFoldedIntoPausedTotal()
    {
        const string json = """{ "Start":"2026-11-20", "Timer":{"StartedAtUtc":"2026-11-22T09:00:00Z","AccumulatedMs":1000,"Running":true} }""";

        var state = new LegacyStudyNoteImporter(new TestClock(new DateOnly(2026, 11, 22))).Convert(json);   // now = 10:00 UTC

        Assert.False(state.Timer!.Running);
        Assert.Equal(1000 + 3_600_000, state.Timer.AccumulatedMs);
    }

    [Fact]
    public void InvalidJson_Throws()
    {
        Assert.Throws<InvalidDataException>(() => new LegacyStudyNoteImporter().Convert("{ nope"));
    }
}
