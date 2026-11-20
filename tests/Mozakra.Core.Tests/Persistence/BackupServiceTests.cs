using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.Core.Tests.Persistence;

public class BackupServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly TestClock _clock = new(new DateOnly(2026, 11, 1));

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void FirstSaveOfDay_CreatesDatedBackup_LaterSavesDoNotOverwriteIt()
    {
        var store = new JsonStateStore(_dir.Path, _clock);

        store.Save(new AppState { Settings = { Language = "ar" } });
        store.Save(new AppState { Settings = { Language = "en" } });

        var backup = Path.Combine(store.Backups.BackupDirectory, "state-2026-11-01.json");
        Assert.True(File.Exists(backup));
        Assert.Equal("ar", StateJson.Deserialize(File.ReadAllText(backup)).Settings.Language);
    }

    [Fact]
    public void Rotation_KeepsNewestSeven()
    {
        var store = new JsonStateStore(_dir.Path, _clock);

        for (var i = 0; i < 10; i++)
        {
            store.Save(new AppState());
            _clock.Today = _clock.Today.AddDays(1);
        }

        var names = store.Backups.GetBackups().Select(Path.GetFileName).ToList();
        Assert.Equal(BackupService.KeepCount, names.Count);
        Assert.Equal("state-2026-11-10.json", names[0]);
        Assert.Equal("state-2026-11-04.json", names[^1]);
    }

    [Fact]
    public void Rotation_IgnoresUnrelatedFiles()
    {
        var store = new JsonStateStore(_dir.Path, _clock);
        Directory.CreateDirectory(store.Backups.BackupDirectory);
        var other = Path.Combine(store.Backups.BackupDirectory, "pre-import-x.json");
        File.WriteAllText(other, "{}");

        for (var i = 0; i < 9; i++)
        {
            store.Save(new AppState());
            _clock.Today = _clock.Today.AddDays(1);
        }

        Assert.True(File.Exists(other));
    }
}
