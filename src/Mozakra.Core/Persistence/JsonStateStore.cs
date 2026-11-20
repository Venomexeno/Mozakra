using System.Globalization;
using System.Text;
using System.Text.Json;
using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

/// <summary>Stores <see cref="AppState"/> in <c>state.json</c> with atomic saves, daily backups and corrupt-file recovery.</summary>
public sealed class JsonStateStore : IStateStore
{
    public const string FileName = "state.json";

    private readonly IClock _clock;
    private readonly object _gate = new();

    public JsonStateStore()
        : this(DefaultDirectory, new SystemClock())
    {
    }

    public JsonStateStore(string dataDirectory, IClock? clock = null)
    {
        DataDirectory = dataDirectory;
        _clock = clock ?? new SystemClock();
        Backups = new BackupService(dataDirectory, _clock);
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozakra");

    public string DataDirectory { get; }
    public string StatePath => Path.Combine(DataDirectory, FileName);
    public BackupService Backups { get; }

    /// <summary>Set when the last <see cref="Load"/> found a corrupt file; holds where it was moved.</summary>
    public string? LastCorruptFilePath { get; private set; }

    /// <summary>
    /// Loads the state. Missing file → new state. Corrupt file → renamed to <c>state.corrupt-*.json</c> and
    /// the newest readable backup is returned (or a new state). Throws
    /// <see cref="UnsupportedSchemaVersionException"/> if the file came from a newer version.
    /// </summary>
    public AppState Load()
    {
        lock (_gate)
        {
            LastCorruptFilePath = null;
            if (!File.Exists(StatePath))
                return new AppState();

            try
            {
                return StateJson.Deserialize(File.ReadAllText(StatePath));
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or DecoderFallbackException)
            {
                LastCorruptFilePath = QuarantineCorruptFile();
                return LoadNewestValidBackup() ?? new AppState();
            }
        }
    }

    public void Save(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            Directory.CreateDirectory(DataDirectory);
            var tmp = StatePath + ".tmp";
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(StateJson.Serialize(state));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(tmp, StatePath, overwrite: true);
            Backups.BackupIfFirstToday(StatePath);
        }
    }

    private string QuarantineCorruptFile()
    {
        var stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        var target = Path.Combine(DataDirectory, $"state.corrupt-{stamp}.json");
        File.Move(StatePath, target, overwrite: true);
        return target;
    }

    private static AppState? TryRead(string path)
    {
        try
        {
            return StateJson.Deserialize(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or DecoderFallbackException
                                       or UnsupportedSchemaVersionException or IOException)
        {
            return null;
        }
    }

    private AppState? LoadNewestValidBackup() =>
        Backups.GetBackups().Select(TryRead).FirstOrDefault(state => state is not null);
}
