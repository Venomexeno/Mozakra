namespace Mozakra.Core.Models;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;
    public List<StudyPlan> Plans { get; set; } = new();
    public WeeklyAvailability Weekly { get; set; } = new();
    public List<AvailabilityException> Exceptions { get; set; } = new();
    public List<LogEntry> Logs { get; set; } = new();
    public ActiveTimer? Timer { get; set; }
    public AppSettings Settings { get; set; } = new();
}
