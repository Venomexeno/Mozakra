using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.Core.Tests.Persistence;

public class ExportImportServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly JsonStateStore _store;
    private readonly ExportImportService _service;

    public ExportImportServiceTests()
    {
        _store = new JsonStateStore(_dir.Path, new TestClock(new DateOnly(2026, 11, 20)));
        _service = new ExportImportService(_store);
    }

    public void Dispose() => _dir.Dispose();

    private static AppState StateWithPlan(string name)
    {
        var plan = new StudyPlan { Name = name };
        return new AppState { Plans = { plan }, Logs = { new LogEntry { PlanId = plan.Id, Minutes = 30 } } };
    }

    [Fact]
    public void ExportThenImport_ReplacesStateAndBacksUpCurrent()
    {
        _store.Save(StateWithPlan("Current"));
        var exportPath = _dir.File("export.json");
        new ExportImportService(new JsonStateStore(_dir.File("other"))).Export(StateWithPlan("Imported"), exportPath);

        var result = _service.Import(exportPath);

        Assert.True(result.Success);
        Assert.Equal("Imported", _store.Load().Plans[0].Name);
        var preImport = Directory.GetFiles(_store.Backups.BackupDirectory, "pre-import-*.json");
        Assert.Equal("Current", StateJson.Deserialize(File.ReadAllText(Assert.Single(preImport))).Plans[0].Name);
    }

    [Fact]
    public void Export_WritesSavedState()
    {
        _store.Save(StateWithPlan("Saved"));
        var path = _dir.File("out.json");

        _service.Export(path);

        Assert.Equal("Saved", StateJson.Deserialize(File.ReadAllText(path)).Plans[0].Name);
    }

    [Theory]
    [InlineData("not json at all", ImportError.InvalidFile)]
    [InlineData("null", ImportError.InvalidFile)]
    [InlineData("""{ "SchemaVersion": 5 }""", ImportError.UnsupportedSchemaVersion)]
    public void Import_RejectsInvalidFile_AndKeepsCurrentState(string content, ImportError expected)
    {
        _store.Save(StateWithPlan("Current"));
        var path = _dir.File("bad.json");
        File.WriteAllText(path, content);

        var result = _service.Import(path);

        Assert.False(result.Success);
        Assert.Equal(expected, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal("Current", _store.Load().Plans[0].Name);
    }

    [Fact]
    public void Import_RejectsLogForUnknownPlan()
    {
        var state = StateWithPlan("X");
        state.Logs.Add(new LogEntry { PlanId = Guid.NewGuid(), Minutes = 5 });
        var path = _dir.File("orphan.json");
        File.WriteAllText(path, StateJson.Serialize(state));

        var result = _service.Import(path);

        Assert.False(result.Success);
        Assert.Equal(ImportError.MissingPlanReference, result.Error);
        Assert.False(File.Exists(_store.StatePath));
    }

    [Fact]
    public void Import_MissingFile_Fails()
    {
        var result = _service.Import(_dir.File("nope.json"));

        Assert.Equal(ImportError.FileNotFound, result.Error);
    }
}
