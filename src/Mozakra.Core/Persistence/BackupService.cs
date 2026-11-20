using System.Globalization;
using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

/// <summary>Keeps one backup per day in <c>backups/state-YYYY-MM-DD.json</c>, newest <see cref="KeepCount"/> only.</summary>
public sealed class BackupService
{
    public const int KeepCount = 7;
    private const string Prefix = "state-";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IClock _clock;

    public BackupService(string dataDirectory, IClock clock)
    {
        BackupDirectory = Path.Combine(dataDirectory, "backups");
        _clock = clock;
    }

    public string BackupDirectory { get; }

    /// <summary>Copies <paramref name="stateFile"/> to today's backup if there isn't one yet, then rotates.</summary>
    public void BackupIfFirstToday(string stateFile)
    {
        if (!File.Exists(stateFile))
            return;

        var target = PathFor(_clock.Today);
        if (File.Exists(target))
            return;

        Directory.CreateDirectory(BackupDirectory);
        File.Copy(stateFile, target, overwrite: false);
        Rotate();
    }

    /// <summary>Daily backups, newest first.</summary>
    public IReadOnlyList<string> GetBackups()
    {
        if (!Directory.Exists(BackupDirectory))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(BackupDirectory, Prefix + "*.json")
            .Select(path => (path, date: TryParseDate(path)))
            .Where(x => x.date is not null)
            .OrderByDescending(x => x.date)
            .Select(x => x.path)
            .ToList();
    }

    /// <summary>Deletes all but the newest <see cref="KeepCount"/> daily backups.</summary>
    public void Rotate()
    {
        foreach (var old in GetBackups().Skip(KeepCount))
            File.Delete(old);
    }

    /// <summary>Copies a file into the backups folder under a one-off name (used before an import).</summary>
    public string BackupBeforeImport(string stateFile)
    {
        Directory.CreateDirectory(BackupDirectory);
        var stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        var target = Path.Combine(BackupDirectory, $"pre-import-{stamp}.json");
        File.Copy(stateFile, target, overwrite: true);
        return target;
    }

    private string PathFor(DateOnly date) =>
        Path.Combine(BackupDirectory, Prefix + date.ToString(DateFormat, CultureInfo.InvariantCulture) + ".json");

    private static DateOnly? TryParseDate(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return DateOnly.TryParseExact(name[Prefix.Length..], DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date : null;
    }
}
