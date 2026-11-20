using System.Text;
using System.Text.Json;
using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

/// <summary>Exports the saved state to a file and imports (validates, backs up, replaces) from one.</summary>
public sealed class ExportImportService
{
    private readonly JsonStateStore _store;

    public ExportImportService(JsonStateStore store) => _store = store;

    /// <summary>Writes the currently saved state to <paramref name="path"/>.</summary>
    public void Export(string path) => Export(_store.Load(), path);

    /// <summary>Writes <paramref name="state"/> to <paramref name="path"/> (for in-memory state).</summary>
    public void Export(AppState state, string path) =>
        File.WriteAllText(path, StateJson.Serialize(state), new UTF8Encoding(false));

    /// <summary>
    /// Validates the file, backs up the current state, then replaces it.
    /// On failure nothing is changed and the result says why.
    /// </summary>
    public ImportResult Import(string path)
    {
        if (!File.Exists(path))
            return ImportResult.Fail(ImportError.FileNotFound, $"File not found: {path}");

        AppState state;
        try
        {
            state = StateJson.Deserialize(File.ReadAllText(path));
        }
        catch (UnsupportedSchemaVersionException ex)
        {
            return ImportResult.Fail(ImportError.UnsupportedSchemaVersion, ex.Message);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or DecoderFallbackException)
        {
            return ImportResult.Fail(ImportError.InvalidFile, ex.Message);
        }

        var missing = FindMissingPlanReference(state);
        if (missing is not null)
            return ImportResult.Fail(ImportError.MissingPlanReference, missing);

        if (File.Exists(_store.StatePath))
            _store.Backups.BackupBeforeImport(_store.StatePath);
        _store.Save(state);
        return ImportResult.Ok(state);
    }

    /// <summary>Describes the first reference to a plan that doesn't exist, or returns null.</summary>
    public static string? FindMissingPlanReference(AppState state)
    {
        var ids = state.Plans.Select(p => p.Id).ToHashSet();

        var log = state.Logs.FirstOrDefault(l => !ids.Contains(l.PlanId));
        if (log is not null)
            return $"Log {log.Id} on {log.Date:yyyy-MM-dd} references unknown plan {log.PlanId}.";

        var plan = state.Plans.FirstOrDefault(p => p.StartsAfterPlanId is { } after && !ids.Contains(after));
        if (plan is not null)
            return $"Plan \"{plan.Name}\" starts after unknown plan {plan.StartsAfterPlanId}.";

        if (state.Timer is { } timer && !ids.Contains(timer.PlanId))
            return $"The timer references unknown plan {timer.PlanId}.";

        return null;
    }
}
