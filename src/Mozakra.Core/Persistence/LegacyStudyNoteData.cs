namespace Mozakra.Core.Persistence;

/// <summary>Shape of the old Study Note <c>data.json</c> (read-only, used by <see cref="LegacyStudyNoteImporter"/>).</summary>
public sealed class LegacyStudyNoteData
{
    public string? Start { get; set; }
    public Dictionary<string, int>? Logs { get; set; }
    public List<string>? Skips { get; set; }
    public Dictionary<string, int>? Units { get; set; }
    public List<string>? Checks { get; set; }
    public LegacyTimerState? Timer { get; set; }
    public bool Topmost { get; set; }
    public bool ShowDetails { get; set; }
    public bool StartupConfigured { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
}
