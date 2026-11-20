using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

public enum ImportError
{
    None,
    FileNotFound,
    InvalidFile,
    UnsupportedSchemaVersion,
    MissingPlanReference,
}

/// <summary>
/// Outcome of <see cref="ExportImportService.Import"/>. The UI localizes <see cref="Error"/>;
/// <see cref="Detail"/> is an English technical hint.
/// </summary>
public sealed record ImportResult(bool Success, ImportError Error, string? Detail, AppState? State)
{
    public static ImportResult Ok(AppState state) => new(true, ImportError.None, null, state);
    public static ImportResult Fail(ImportError error, string detail) => new(false, error, detail, null);
}
